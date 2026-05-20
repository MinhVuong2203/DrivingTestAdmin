using Backend.DTO;
using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;
using System.Text.Json;

namespace Backend.Service
{
    public class DrivingCenterImportService : IDrivingCenterImportService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly DrivingCenterRepository _drivingCenterRepository;

        public DrivingCenterImportService(
            HttpClient httpClient,
            IConfiguration configuration,
            DrivingCenterRepository drivingCenterRepository)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _drivingCenterRepository = drivingCenterRepository;
        }

        public async Task<ImportDrivingCenterResult> ImportFromLocalBusinessData(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Query không được để trống.");
            }

            string host = _configuration["RapidApi:LocalBusinessDataHost"] ?? "";
            string key = _configuration["RapidApi:LocalBusinessDataKey"] ?? "";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(key))
            {
                throw new Exception("Thiếu cấu hình RapidAPI host hoặc key.");
            }

            string encodedQuery = Uri.EscapeDataString(query.Trim());

            string url = $"https://local-business-data.p.rapidapi.com/search?query={encodedQuery}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Add("x-rapidapi-host", host);
            request.Headers.Add("x-rapidapi-key", key);

            var response = await _httpClient.SendAsync(request);

            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Gọi Local Business Data thất bại. Status: {(int)response.StatusCode}. Body: {responseBody}");
            }

            using JsonDocument document = JsonDocument.Parse(responseBody);

            if (!document.RootElement.TryGetProperty("data", out JsonElement dataElement)
                || dataElement.ValueKind != JsonValueKind.Array)
            {
                throw new Exception("Response từ API không có mảng data.");
            }

            List<DrivingCenter> centers = new();

            foreach (JsonElement item in dataElement.EnumerateArray())
            {
                var center = MapToDrivingCenter(item, query);
                centers.Add(center);
            }

            int savedCount = await _drivingCenterRepository.AddMany(centers);
            Console.WriteLine($"Đã insert/update {savedCount} trung tâm vào Firestore.");
            return new ImportDrivingCenterResult
            {
                message = "Import dữ liệu trung tâm đào tạo lái xe thành công.",
                total_from_api = centers.Count,
                saved_count = savedCount
            };
        }

        private DrivingCenter MapToDrivingCenter(JsonElement json, string query)
        {
            string photoUrl = "";

            if (json.TryGetProperty("photos_sample", out JsonElement photosElement)
                && photosElement.ValueKind == JsonValueKind.Array
                && photosElement.GetArrayLength() > 0)
            {
                JsonElement firstPhoto = photosElement[0];

                if (firstPhoto.TryGetProperty("photo_url", out JsonElement photoUrlElement))
                {
                    photoUrl = GetString(photoUrlElement);
                }
            }

            return new DrivingCenter
            {
                name = GetString(json, "name"),
                phone_number = GetString(json, "phone_number"),
                photo_url = photoUrl,
                website = GetString(json, "website"),
                rating = GetDouble(json, "rating"),
                review_count = GetInt(json, "review_count"),
                business_status = GetString(json, "business_status"),
                address = GetString(json, "address"),
                district = GetString(json, "district"),
                city = GetString(json, "city"),
                opening_status = GetString(json, "opening_status"),
                search_query = query,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };
        }

        private string GetString(JsonElement json, string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out JsonElement element))
                return "";

            return GetString(element);
        }

        private string GetString(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
                return element.GetString() ?? "";

            if (element.ValueKind == JsonValueKind.Number ||
                element.ValueKind == JsonValueKind.True ||
                element.ValueKind == JsonValueKind.False)
                return element.ToString();

            return "";
        }

        private double GetDouble(JsonElement json, string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out JsonElement element))
                return 0;

            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double value))
                return value;

            if (element.ValueKind == JsonValueKind.String &&
                double.TryParse(element.GetString(), out double stringValue))
                return stringValue;

            return 0;
        }

        private int GetInt(JsonElement json, string propertyName)
        {
            if (!json.TryGetProperty(propertyName, out JsonElement element))
                return 0;

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int value))
                return value;

            if (element.ValueKind == JsonValueKind.String &&
                int.TryParse(element.GetString(), out int stringValue))
                return stringValue;

            return 0;
        }

        public Task<List<DrivingCenter>> Search(string? keyword)
        {
            throw new NotImplementedException();
        }

        public Task<DrivingCenter?> GetById(string id)
        {
            throw new NotImplementedException();
        }
    }
}
