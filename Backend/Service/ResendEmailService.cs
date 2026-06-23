using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Backend.Service.Interface;

namespace Backend.Service
{
    public class ResendEmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ResendEmailService> _logger;

        public ResendEmailService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ResendEmailService> logger
        )
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendOtpEmail(
            string receiverEmail,
            string otp,
            int expireMinutes,
            CancellationToken cancellationToken = default
        )
        {
            var apiKey =
                _configuration["Resend:ApiKey"]
                ?? throw new InvalidOperationException(
                    "Chưa cấu hình Resend:ApiKey"
                );

            var from =
                _configuration["Resend:From"]
                ?? "Driving Test <onboarding@resend.dev>";

            if (string.IsNullOrWhiteSpace(receiverEmail))
            {
                throw new ArgumentException(
                    "Email người nhận không hợp lệ"
                );
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Resend API key đang trống"
                );
            }

            var payload = new
            {
                from,
                to = new[]
                {
                    receiverEmail
                },
                subject = "Mã OTP xác thực tài khoản Driving Test",
                html = BuildOtpHtml(
                    otp,
                    expireMinutes
                ),
                text =
                    $"Mã OTP của bạn là {otp}. " +
                    $"Mã có hiệu lực trong {expireMinutes} phút."
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.resend.com/emails"
            );

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey
                );

            request.Headers.Add(
                "Idempotency-Key",
                $"otp-{receiverEmail}-{otp}"
            );

            request.Content =
                JsonContent.Create(payload);

            _logger.LogInformation(
                "Đang gửi OTP qua Resend đến {Email}",
                receiverEmail
            );

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken
                );

            var responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken
                );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Resend trả lỗi {StatusCode}: {Response}",
                    response.StatusCode,
                    responseBody
                );

                throw new InvalidOperationException(
                    $"Không thể gửi email OTP qua Resend: " +
                    $"{responseBody}"
                );
            }

            string? emailId = null;

            try
            {
                using var json =
                    JsonDocument.Parse(responseBody);

                if (json.RootElement.TryGetProperty(
                        "id",
                        out var idElement
                    ))
                {
                    emailId =
                        idElement.GetString();
                }
            }
            catch (JsonException)
            {
                // Không ảnh hưởng việc gửi mail.
            }

            _logger.LogInformation(
                "Resend đã nhận yêu cầu gửi OTP đến {Email}. EmailId={EmailId}",
                receiverEmail,
                emailId
            );
        }

        private static string BuildOtpHtml(
            string otp,
            int expireMinutes
        )
        {
            return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport"
                      content="width=device-width, initial-scale=1.0">
            </head>

            <body style="
                margin:0;
                padding:24px;
                background:#f1f5f9;
                font-family:Arial,sans-serif;
            ">
                <div style="
                    max-width:520px;
                    margin:auto;
                    padding:32px;
                    background:#ffffff;
                    border-radius:16px;
                    box-shadow:0 8px 30px rgba(15,23,42,0.08);
                ">
                    <h2 style="
                        margin-top:0;
                        color:#0f172a;
                    ">
                        Xác thực tài khoản Driving Test
                    </h2>

                    <p style="
                        color:#475569;
                        line-height:1.6;
                    ">
                        Mã OTP xác thực tài khoản của bạn là:
                    </p>

                    <div style="
                        margin:22px 0;
                        padding:18px;
                        background:#eff6ff;
                        border:1px solid #bfdbfe;
                        border-radius:12px;
                        text-align:center;
                        color:#1d4ed8;
                        font-size:34px;
                        font-weight:800;
                        letter-spacing:10px;
                    ">
                        {{otp}}
                    </div>

                    <p style="
                        color:#64748b;
                        line-height:1.6;
                    ">
                        Mã có hiệu lực trong
                        <strong>{{expireMinutes}} phút</strong>.
                    </p>

                    <p style="
                        margin-bottom:0;
                        color:#94a3b8;
                        font-size:13px;
                    ">
                        Không chia sẻ mã OTP này cho bất kỳ ai.
                        Nếu bạn không thực hiện yêu cầu này,
                        hãy bỏ qua email.
                    </p>
                </div>
            </body>
            </html>
            """;
        }
    }
}