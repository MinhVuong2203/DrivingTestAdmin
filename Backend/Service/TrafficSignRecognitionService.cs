using System.Text;
using System.Text.Json;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class TrafficSignRecognitionService : ITrafficSignRecognitionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public TrafficSignRecognitionService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> RecognizeTrafficSign(
            string base64Image,
            string? mimeType = null)
        {
            if (string.IsNullOrWhiteSpace(base64Image))
            {
                return "Không có ảnh để nhận diện";
            }

            var apiKey = _configuration["GeminiTrafficSignRecognition:ApiKey"];
            var model =
                _configuration["GeminiTrafficSignRecognition:Model"] ?? "gemini-3.1-flash-lite-preview";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "Thiếu Gemini API key";
            }

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = BuildPrompt() },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = string.IsNullOrWhiteSpace(mimeType)
                                        ? "image/jpeg"
                                        : mimeType,
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.4,
                    maxOutputTokens = 500
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

                if ((int)response.StatusCode == 429)
                {
                    return "Vượt quá hạn mức sử dụng API. Vui lòng thử lại sau.";
                }

                if (!response.IsSuccessStatusCode)
                {
                    return $"Loi: {ExtractGeminiError(responseText)}";
                }

                using var doc = JsonDocument.Parse(responseText);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return string.IsNullOrWhiteSpace(text)
                    ? "Không nhận diện được biển báo"
                    : text;
            }
            catch (Exception ex)
            {
                return $"Lỗi kết nối: {ex.Message}";
            }
        }

        private static string ExtractGeminiError(string responseText)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseText);
                return doc.RootElement
                    .GetProperty("error")
                    .GetProperty("message")
                    .GetString() ?? "Unknown error";
            }
            catch
            {
                return "Unknown error";
            }
        }

        private static string BuildPrompt()
        {
            return """
                Bạn là chuyên gia nhận diện biển báo giao thông Việt Nam.

                Phân tích ảnh biển báo và trả về thông tin theo format sau:

                TÊN BIỂN BÁO
                [Tên chính xác của biển báo theo quy chuẩn Việt Nam]

                LOẠI BIỂN BÁO
                [Biển cấm / Biển nguy hiểm / Biển chỉ dẫn / Biển hiệu lệnh]

                Ý NGHĨA
                [Giải thích ý nghĩa của biển báo]

                THÔNG SỐ
                [Các số liệu nếu có: tốc độ, khoảng cách, trọng tải, v.v...]

                LƯU Ý
                [Ghi chú quan trọng nếu có]

                ---
                Nếu không nhận diện được hoặc không phải biển báo giao thông, chỉ trả về: "Không nhận diện được biển báo"
                """;
        }
    }
}
