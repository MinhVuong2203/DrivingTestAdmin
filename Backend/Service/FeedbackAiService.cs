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
        public FeedbackAiService(
            HttpClient httpClient,
            IConfiguration configuration)
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
            FeedbackAiReplyRequest request)
        {
            var content = request.Content?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(content))
            {
                return new FeedbackAiReplyResponse
                {
                    ReplyText = "Cảm ơn bạn đã liên hệ. Bạn vui lòng gửi thêm nội dung chi tiết để đội ngũ hỗ trợ kiểm tra tốt hơn.",
                    SpamRisk = false,
                    Source = "fallback"
                };
            }

            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(content))
            {
                return fallback;
            }

            var prompt = BuildPrompt(request);
            var payload = new
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return BuildFallbackResponse(content, "fallback_no_api_key");
            }

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var prompt = BuildPrompt(request);
            var body = new
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
                    temperature = 0.25,
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
                var response = await _httpClient.PostAsync(
                    url,
                    new StringContent(
                        JsonSerializer.Serialize(body),
                        Encoding.UTF8,
                        "application/json")
                );

                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return BuildFallbackResponse(
                        content,
                        $"fallback_ai_{(int)response.StatusCode}");
                }

                using var doc = JsonDocument.Parse(responseText);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrWhiteSpace(text))
                {
                    return BuildFallbackResponse(content, "fallback_empty_ai");
                }

                using var resultDoc = JsonDocument.Parse(text);
                var root = resultDoc.RootElement;

                return new FeedbackAiReplyResponse
                {
                    ReplyText = ReadString(root, "replyText"),
                    SpamRisk = root.TryGetProperty("spamRisk", out var spamRisk)
                        && spamRisk.GetBoolean(),
                    SpamReason = ReadString(root, "spamReason"),
                    Source = "ai"
                };
            }
            catch
            {
                return fallback;
                return BuildFallbackResponse(content, "fallback_exception");
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
            return $$"""
                Bạn là trợ lý chăm sóc người dùng cho ứng dụng ôn thi GPLX.

                Nhiệm vụ:
                - Đọc phản hồi của user.
                - Soạn câu trả lời tiếng Việt lịch sự, ngắn gọn, có hướng xử lý cụ thể.
                - Không hứa chắc thời gian sửa lỗi.
                - Nếu nội dung có dấu hiệu spam, quảng cáo, lặp vô nghĩa, link lừa đảo hoặc chửi bậy, đặt spamRisk=true và trả lời trung tính.

                Chỉ trả về JSON đúng format:
                {
                  "replyText": "nội dung trả lời cho user",
                  "spamRisk": true/false,
                  "spamReason": "lý do ngắn gọn, để rỗng nếu không nghi spam"
                }

                Thông tin user:
                - Tên: {{request.DisplayName}}
                - Email: {{request.Email}}
                - Nền tảng: {{request.Platform}}

                Nội dung phản hồi:
                {{request.Content}}
                """;
        }

        private static FeedbackAiReplyResponse BuildFallbackResponse(
            string content,
            string source)
        {
            var normalized = content.ToLowerInvariant();
            var spamRisk =
                normalized.Length < 8
                || normalized.Contains("http://")
                || normalized.Contains("https://")
                || normalized.Contains("www.")
                || normalized.Contains("telegram")
                || normalized.Contains("khuyến mãi")
                || normalized.Contains("spam");

            return new FeedbackAiReplyResponse
            {
                ReplyText = spamRisk
                    ? "Cảm ơn bạn đã gửi phản hồi. Nội dung này đã được hệ thống ghi nhận và sẽ được kiểm tra trước khi xử lý."
                    : "Cảm ơn bạn đã gửi phản hồi. Đội ngũ GPLX đã ghi nhận nội dung này và sẽ kiểm tra để cải thiện ứng dụng trong các bản cập nhật tới.",
                SpamRisk = spamRisk,
                SpamReason = spamRisk
                    ? "Nội dung ngắn, chứa liên kết hoặc có dấu hiệu quảng cáo/lặp."
                    : "",
                Source = source
            };
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var value)
                ? value.GetString() ?? ""
                : "";
        }
    }
}
