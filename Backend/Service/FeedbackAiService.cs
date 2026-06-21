using System.Text;
using System.Text.Json;
using Backend.DTO;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class FeedbackAiService : IFeedbackAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public FeedbackAiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<FeedbackAiReplyResponse> GenerateReplyAsync(
            FeedbackAiReplyRequest request,
            CancellationToken cancellationToken = default)
        {
            var content = request.Content?.Trim() ?? "";
            var localSpamReason = LocalSpamReason(content);
            var fallback = BuildFallbackReply(request, localSpamReason);

            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(content))
            {
                return fallback;
            }

            var prompt = BuildPrompt(request);
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.35,
                    responseMimeType = "application/json"
                }
            };

            try
            {
                var url =
                    $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var json = JsonSerializer.Serialize(payload);
                using var response = await _httpClient.PostAsync(
                    url,
                    new StringContent(json, Encoding.UTF8, "application/json"),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return fallback;
                }

                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                var candidateText = ExtractCandidateText(responseText);
                if (string.IsNullOrWhiteSpace(candidateText))
                {
                    return fallback;
                }

                using var resultDoc = JsonDocument.Parse(candidateText);
                var root = resultDoc.RootElement;
                var replyText = ReadString(root, "replyText");
                if (string.IsNullOrWhiteSpace(replyText))
                {
                    return fallback;
                }

                var aiSpamRisk = ReadBool(root, "spamRisk");
                var aiSpamReason = ReadString(root, "spamReason");

                return new FeedbackAiReplyResponse
                {
                    ReplyText = replyText.Trim(),
                    SpamRisk = fallback.SpamRisk || aiSpamRisk,
                    SpamReason = fallback.SpamRisk ? fallback.SpamReason : aiSpamReason,
                    Source = "ai"
                };
            }
            catch
            {
                return fallback;
            }
        }

        private static string BuildPrompt(FeedbackAiReplyRequest request)
        {
            var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? "người dùng"
                : request.DisplayName.Trim();
            var platform = string.IsNullOrWhiteSpace(request.Platform)
                ? "không rõ"
                : request.Platform.Trim();

            return $$"""
                Bạn là trợ lý chăm sóc người dùng cho app ôn thi lái xe "Kiến thức lái xe 600".

                Hãy trả lời phản hồi của người dùng bằng tiếng Việt, giọng lịch sự, ngắn gọn, tự nhiên.
                Nếu người dùng báo lỗi hoặc app chậm, hãy xin lỗi, ghi nhận, nói đội ngũ sẽ kiểm tra và gợi ý thử cập nhật app/khởi động lại nếu phù hợp.
                Nếu người dùng khen, hãy cảm ơn.
                Nếu nội dung có dấu hiệu spam, xúc phạm, vô nghĩa hoặc quảng cáo, vẫn trả lời lịch sự nhưng đánh dấu spamRisk = true.

                Chỉ trả về JSON đúng format:
                {
                  "replyText": "câu trả lời gửi cho người dùng",
                  "spamRisk": true/false,
                  "spamReason": "lý do ngắn nếu có"
                }

                Tên người dùng: {{displayName}}
                Nền tảng: {{platform}}
                Nội dung phản hồi:
                {{request.Content?.Trim() ?? ""}}
                """;
        }

        private static FeedbackAiReplyResponse BuildFallbackReply(
            FeedbackAiReplyRequest request,
            string spamReason)
        {
            var name = string.IsNullOrWhiteSpace(request.DisplayName)
                ? "bạn"
                : request.DisplayName.Trim();

            return new FeedbackAiReplyResponse
            {
                ReplyText =
                    $"Cảm ơn {name} đã gửi góp ý. Đội ngũ quản trị đã ghi nhận phản hồi này và sẽ kiểm tra để cải thiện ứng dụng trong thời gian sớm nhất.",
                SpamRisk = !string.IsNullOrWhiteSpace(spamReason),
                SpamReason = spamReason,
                Source = "fallback"
            };
        }

        private static string LocalSpamReason(string content)
        {
            var normalized = Normalize(content);
            if (normalized.Length < 10)
            {
                return "Nội dung quá ngắn.";
            }

            var urlCount = normalized.Split("http", StringSplitOptions.None).Length - 1;
            if (urlCount >= 2 || normalized.Contains("bit.ly") || normalized.Contains("t.me/"))
            {
                return "Nội dung có dấu hiệu quảng cáo hoặc spam liên kết.";
            }

            var distinctChars = normalized.Where(char.IsLetterOrDigit).Distinct().Count();
            if (normalized.Length >= 20 && distinctChars <= 3)
            {
                return "Nội dung lặp ký tự bất thường.";
            }

            return "";
        }

        private static string ExtractCandidateText(string responseText)
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;
            return root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property)
                ? property.GetString() ?? ""
                : "";
        }

        private static bool ReadBool(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.True;
        }

        private static string Normalize(string value)
        {
            return string.Join(
                " ",
                value.Trim().ToLowerInvariant().Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
