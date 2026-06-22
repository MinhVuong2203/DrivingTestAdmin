using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            var fallback = BuildFallbackReply(request, LocalSpamReason(content));
            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(content))
            {
                return fallback;
            }

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = BuildPrompt(request) }
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
                var response = await _httpClient.PostAsync(
                    url,
                    new StringContent(
                        JsonSerializer.Serialize(body),
                        Encoding.UTF8,
                        "application/json"),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return fallback;
                }

                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                var aiText = ExtractAiText(responseText);
                if (string.IsNullOrWhiteSpace(aiText))
                {
                    return fallback;
                }

                using var resultDoc = JsonDocument.Parse(aiText);
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
            var content = request.Content?.Trim() ?? "";

            return $$"""
                Bạn là trợ lý chăm sóc người dùng cho app ôn thi lái xe "Kiến thức lái xe 600".
                Hãy trả lời bằng tiếng Việt có dấu, lịch sự, ngắn gọn và tự nhiên.
                Tuyệt đối không trả lời tiếng Việt không dấu.
                Nếu người dùng báo lỗi hoặc app chậm, hãy xin lỗi, ghi nhận và nói đội ngũ sẽ kiểm tra.
                Nếu nội dung có dấu hiệu spam, quảng cáo, xúc phạm hoặc vô nghĩa, đánh dấu spamRisk = true.

                Chỉ trả về JSON đúng format:
                {
                  "replyText": "câu trả lời gửi cho người dùng bằng tiếng Việt có dấu",
                  "spamRisk": true/false,
                  "spamReason": "lý do ngắn nếu có"
                }

                Tên người dùng: {{displayName}}
                Nền tảng: {{platform}}
                Nội dung phản hồi:
                {{content}}
                """;
        }

        private static FeedbackAiReplyResponse BuildFallbackReply(
            FeedbackAiReplyRequest request,
            string spamReason)
        {
            var name = string.IsNullOrWhiteSpace(request.DisplayName)
                ? "bạn"
                : request.DisplayName.Trim();
            var isSpam = !string.IsNullOrWhiteSpace(spamReason);

            return new FeedbackAiReplyResponse
            {
                ReplyText = isSpam
                    ? "Phản hồi không phù hợp."
                    : $"Cảm ơn {name} đã gửi góp ý. Đội ngũ quản trị đã ghi nhận phản hồi này và sẽ kiểm tra để cải thiện ứng dụng trong thời gian sớm nhất.",
                SpamRisk = isSpam,
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

            if (ContainsOffensiveOrNegativeTerm(content))
            {
                return "Nội dung có từ ngữ phản cảm hoặc tiêu cực.";
            }

            if (normalized.Contains("bit.ly") || normalized.Contains("t.me/"))
            {
                return "Nội dung có dấu hiệu spam liên kết.";
            }

            var distinctChars = normalized.Where(char.IsLetterOrDigit).Distinct().Count();
            if (normalized.Length >= 20 && distinctChars <= 3)
            {
                return "Nội dung lặp ký tự bất thường.";
            }

            return "";
        }

        private static bool ContainsOffensiveOrNegativeTerm(string content)
        {
            var original = Normalize(content);
            var folded = FoldVietnamese(original);
            var padded = $" {folded} ";

            var phraseTerms = new[]
            {
                "dit me",
                "du ma",
                "me may",
                "oc cho",
                "nhu lon",
                "nhu cc",
                "nhu cut",
                "vo dung",
                "do rac",
                "rac ruoi",
                "lam an nhu",
                "app rac"
            };

            if (phraseTerms.Any(term => padded.Contains($" {term} ")))
            {
                return true;
            }

            var tokenTerms = new HashSet<string>(StringComparer.Ordinal)
            {
                "dm",
                "dmm",
                "dkm",
                "clgt",
                "vcl",
                "vl",
                "dit",
                "du",
                "lon",
                "buoi",
                "deo",
                "ngu",
                "dot",
                "fuck",
                "shit"
            };

            if (folded.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(token => tokenTerms.Contains(token)))
            {
                return true;
            }

            var accentedTerms = new[]
            {
                "\u0111\u1ecbt",
                "\u0111\u1ee5",
                "l\u1ed3n",
                "bu\u1ed3i",
                "\u0111\u00e9o",
                "\u00f3c ch\u00f3"
            };

            return accentedTerms.Any(original.Contains);
        }

        private static string ExtractAiText(string responseText)
        {
            using var doc = JsonDocument.Parse(responseText);
            return doc.RootElement
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
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));
        }

        private static string FoldVietnamese(string value)
        {
            var replaced = value
                .Replace('\u0111', 'd')
                .Replace('\u0110', 'D');
            var formD = replaced.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(formD.Length);

            foreach (var character in formD)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo
                    .GetUnicodeCategory(character);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return Regex.Replace(
                builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant(),
                @"\s+",
                " ").Trim();
        }
    }
}
