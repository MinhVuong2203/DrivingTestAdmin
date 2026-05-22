using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.DTO;
using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;
using Google.Cloud.Firestore;

namespace Backend.Service
{
    public class PayOsPaymentService : IPayOsPaymentService
    {
        private const string PayOsBaseUrl = "https://api-merchant.payos.vn";

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly FirestoreDb _db;
        private readonly VipPackageRepository _vipPackageRepository;
        private readonly PaymentOrderRepository _paymentOrderRepository;

        public PayOsPaymentService(
            HttpClient httpClient,
            IConfiguration configuration,
            FirestoreDb db,
            VipPackageRepository vipPackageRepository,
            PaymentOrderRepository paymentOrderRepository)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _db = db;
            _vipPackageRepository = vipPackageRepository;
            _paymentOrderRepository = paymentOrderRepository;
        }

        public async Task<PayOsPaymentResponse> CreatePaymentAsync(CreatePayOsPaymentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
                throw new ArgumentException("UserId không được để trống");

            if (string.IsNullOrWhiteSpace(request.PackageId))
                throw new ArgumentException("PackageId không được để trống");

            var package = await _vipPackageRepository.GetByIdAsync(request.PackageId)
                ?? throw new ArgumentException("Không tìm thấy gói VIP");

            if (!package.IsActive)
                throw new ArgumentException("Gói VIP đang tạm ngưng");

            var amount = Convert.ToInt32(Math.Round(package.VipPrice, MidpointRounding.AwayFromZero));
            if (amount <= 0)
                throw new ArgumentException("Giá gói VIP không hợp lệ");

            var orderCode = GenerateOrderCode();
            var returnUrl = GetRequiredConfig("PayOS:ReturnUrl");
            var cancelUrl = GetRequiredConfig("PayOS:CancelUrl");
            var description = BuildDescription(package.VipName);
            var signature = CreatePaymentSignature(amount, cancelUrl, description, orderCode, returnUrl);

            var payload = new
            {
                orderCode,
                amount,
                description,
                returnUrl,
                cancelUrl,
                signature,
                items = new[]
                {
                    new
                    {
                        name = package.VipName,
                        quantity = 1,
                        price = amount
                    }
                }
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{PayOsBaseUrl}/v2/payment-requests");
            requestMessage.Headers.Add("x-client-id", GetRequiredConfig("PayOS:ClientId"));
            requestMessage.Headers.Add("x-api-key", GetRequiredConfig("PayOS:ApiKey"));
            requestMessage.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(requestMessage);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"PayOS trả lỗi: {responseBody}");

            using var json = JsonDocument.Parse(responseBody);
            var root = json.RootElement;
            var code = root.GetProperty("code").GetString();

            if (code != "00")
            {
                var desc = root.TryGetProperty("desc", out var descEl) ? descEl.GetString() : "unknown";
                throw new InvalidOperationException($"PayOS không tạo được link: {desc}");
            }

            var data = root.GetProperty("data");
            var paymentLinkId = data.GetProperty("paymentLinkId").GetString() ?? string.Empty;
            var checkoutUrl = data.GetProperty("checkoutUrl").GetString() ?? string.Empty;
            var status = data.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString() ?? "PENDING"
                : "PENDING";

            await _paymentOrderRepository.CreateAsync(new PaymentOrder
            {
                OrderCode = orderCode,
                PaymentLinkId = paymentLinkId,
                CheckoutUrl = checkoutUrl,
                UserId = request.UserId,
                PackageId = package.Id ?? request.PackageId,
                PackageName = package.VipName,
                Amount = amount,
                Status = status
            });

            return new PayOsPaymentResponse
            {
                OrderCode = orderCode,
                PaymentLinkId = paymentLinkId,
                CheckoutUrl = checkoutUrl,
                Status = status,
                Amount = amount,
                PackageId = package.Id ?? request.PackageId,
                PackageName = package.VipName
            };
        }

        public async Task<bool> HandleWebhookAsync(JsonElement payload)
        {
            if (!payload.TryGetProperty("data", out var data) ||
                !payload.TryGetProperty("signature", out var signatureElement))
            {
                return false;
            }

            var signature = signatureElement.GetString() ?? string.Empty;
            if (!VerifyDataSignature(data, signature))
                return false;

            var code = GetString(data, "code");
            var orderCode = GetInt64(data, "orderCode");

            if (code == "00")
            {
                await ActivateVipAsync(orderCode);
                return true;
            }

            if (orderCode > 0)
                await _paymentOrderRepository.UpdateStatusAsync(orderCode, code);

            return true;
        }

        public async Task<bool> SyncPaymentStatusAsync(long orderCode)
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{PayOsBaseUrl}/v2/payment-requests/{orderCode}");
            requestMessage.Headers.Add("x-client-id", GetRequiredConfig("PayOS:ClientId"));
            requestMessage.Headers.Add("x-api-key", GetRequiredConfig("PayOS:ApiKey"));

            using var response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode) return false;

            var responseBody = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(responseBody);
            var root = json.RootElement;

            if (root.GetProperty("code").GetString() != "00") return false;

            var data = root.GetProperty("data");
            var status = GetString(data, "status");

            if (status == "PAID")
            {
                await ActivateVipAsync(orderCode);
                return true;
            }

            await _paymentOrderRepository.UpdateStatusAsync(orderCode, status);
            return false;
        }

        private async Task ActivateVipAsync(long orderCode)
        {
            var order = await _paymentOrderRepository.GetByOrderCodeAsync(orderCode);
            if (order == null || order.Status == "PAID") return;

            var package = await _vipPackageRepository.GetByIdAsync(order.PackageId);
            if (package == null) return;

            var startDate = DateTime.UtcNow;
            var vipData = new Dictionary<string, object>
            {
                { "packageId", order.PackageId },
                { "name", order.PackageName },
                { "startDate", startDate }
            };

            if (!package.IsPeriod)
            {
                var vipDays = package.VipTime.GetValueOrDefault();
                vipData["endDate"] = startDate.AddDays(vipDays);
            }

            await _db.Collection("users")
                .Document(order.UserId)
                .SetAsync(new Dictionary<string, object>
                {
                    { "vip", vipData }
                }, SetOptions.MergeAll);

            await _paymentOrderRepository.MarkPaidAsync(orderCode);
        }

        private string GetRequiredConfig(string key)
        {
            return _configuration[key]
                ?? throw new InvalidOperationException($"Thiếu cấu hình {key}");
        }

        private string CreatePaymentSignature(int amount, string cancelUrl, string description, long orderCode, string returnUrl)
        {
            var data = $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
            return HmacSha256(data);
        }

        private bool VerifyDataSignature(JsonElement data, string signature)
        {
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (var property in data.EnumerateObject())
            {
                fields[property.Name] = JsonElementToSignatureValue(property.Value);
            }

            var signedData = string.Join("&", fields.Select(item => $"{item.Key}={item.Value}"));
            var expected = HmacSha256(signedData);
            return string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase);
        }

        private string HmacSha256(string data)
        {
            var key = Encoding.UTF8.GetBytes(GetRequiredConfig("PayOS:ChecksumKey"));
            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static long GenerateOrderCode()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000
                + Random.Shared.Next(0, 1000);
        }

        private static string BuildDescription(string packageName)
        {
            var description = $"VIP {packageName}".Trim();
            return description.Length <= 25 ? description : description[..25];
        }

        private static long GetInt64(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value)) return 0;

            return value.ValueKind switch
            {
                JsonValueKind.Number => value.GetInt64(),
                JsonValueKind.String when long.TryParse(value.GetString(), out var result) => result,
                _ => 0
            };
        }

        private static string GetString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value)) return string.Empty;
            return JsonElementToSignatureValue(value);
        }

        private static string JsonElementToSignatureValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => value.GetRawText()
            };
        }
    }
}
