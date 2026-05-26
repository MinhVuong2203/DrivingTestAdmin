using System.Text;
using System.Text.Json;
using Backend.Models;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class AiModerationService : IAiModerationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public AiModerationService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<bool> IsPostViolatedByAi(string content)
        {
            var result = await CheckPostByAi(content);
            return result.violated;
        }

        public async Task<AiModerationResult> CheckPostByAi(string content)
        {
            var result = new AiModerationResult
            {
                violated = false,
                reason = "",
                source = "ai",
                rawResponse = ""
            };

            if (string.IsNullOrWhiteSpace(content))
            {
                result.reason = "Nội dung rỗng";
                return result;
            }

            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                result.source = "ai_error";
                result.reason = "Thiếu Gemini API key";
                return result;
            }

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var prompt = """
                Bạn là hệ thống kiểm duyệt nội dung cho mạng xã hội học lái xe.

                Nội dung vi phạm gồm:
                - Tục tĩu, chửi bậy, xúc phạm cá nhân
                - Kích động bạo lực
                - Quấy rối, đe dọa
                - Nội dung spam, lừa đảo
                - Nội dung 18+
                - Ngôn từ thù ghét
                - Nội dung không phù hợp môi trường học tập

                Chỉ trả về JSON đúng format:
                {"violated": true/false, "reason": "lý do ngắn gọn"}

                Nội dung cần kiểm tra:
                """ + content;

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
                    temperature = 0,
                    responseMimeType = "application/json"
                }
            };

            var json = JsonSerializer.Serialize(body);

            try
            {
                var response = await _httpClient.PostAsync(
                    url,
                    new StringContent(json, Encoding.UTF8, "application/json")
                );

                var responseText = await response.Content.ReadAsStringAsync();

                result.rawResponse = responseText;

                if (!response.IsSuccessStatusCode)
                {
                    result.source = "ai_error";
                    result.reason = $"Gemini API lỗi: {(int)response.StatusCode}";
                    return result;
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
                    result.source = "ai_error";
                    result.reason = "AI không trả về text";
                    return result;
                }

                using var resultDoc = JsonDocument.Parse(text);

                result.violated = resultDoc.RootElement.GetProperty("violated").GetBoolean();
                result.reason = resultDoc.RootElement.TryGetProperty("reason", out var reason)
                    ? reason.GetString() ?? ""
                    : "";

                return result;
            }
            catch (Exception ex)
            {
                result.source = "ai_exception";
                result.reason = ex.Message;
                return result;
            }
        }
    }
}