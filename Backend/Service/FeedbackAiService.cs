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
                ? "nguoi dung"
                : request.DisplayName.Trim();
            var platform = string.IsNullOrWhiteSpace(request.Platform)
                ? "khong ro"
                : request.Platform.Trim();
            var content = request.Content?.Trim() ?? "";

            return $$"""
                Ban la tro ly cham soc nguoi dung cho app on thi lai xe "Kien thuc lai xe 600".
                Hay tra loi bang tieng Viet co dau, lich su, ngan gon va tu nhien.
                Neu nguoi dung bao loi hoac app cham, hay xin loi, ghi nhan va noi doi ngu se kiem tra.
                Neu noi dung co dau hieu spam, quang cao, xuc pham hoac vo nghia, danh dau spamRisk = true.

                Chi tra ve JSON dung format:
                {
                  "replyText": "cau tra loi gui cho nguoi dung",
                  "spamRisk": true/false,
                  "spamReason": "ly do ngan neu co"
                }

                Ten nguoi dung: {{displayName}}
                Nen tang: {{platform}}
                Noi dung phan hoi:
                {{content}}
                """;
        }

        private static FeedbackAiReplyResponse BuildFallbackReply(
            FeedbackAiReplyRequest request,
            string spamReason)
        {
            var name = string.IsNullOrWhiteSpace(request.DisplayName)
                ? "ban"
                : request.DisplayName.Trim();

            return new FeedbackAiReplyResponse
            {
                ReplyText =
                    $"Cam on {name} da gui gop y. Doi ngu quan tri da ghi nhan phan hoi nay va se kiem tra de cai thien ung dung trong thoi gian som nhat.",
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
                return "Noi dung qua ngan.";
            }

            if (normalized.Contains("bit.ly") || normalized.Contains("t.me/"))
            {
                return "Noi dung co dau hieu spam lien ket.";
            }

            var distinctChars = normalized.Where(char.IsLetterOrDigit).Distinct().Count();
            if (normalized.Length >= 20 && distinctChars <= 3)
            {
                return "Noi dung lap ky tu bat thuong.";
            }

            return "";
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
    }
}
