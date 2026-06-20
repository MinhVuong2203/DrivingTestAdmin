using Backend.DTO;
using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;
using System.Security.Cryptography;
using System.Text;
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

            var searchResult = await SearchLocalBusinessDataPaged(query, page: 1, pageSize: 50);
            int savedCount = await _drivingCenterRepository.AddMany(searchResult.data);
            Console.WriteLine($"Đã insert/update {savedCount} trung tâm vào Firestore.");

            return new ImportDrivingCenterResult
            {
                message = "Import dữ liệu trung tâm đào tạo lái xe thành công.",
                total_from_api = searchResult.data.Count,
                saved_count = savedCount
            };
        }

        public async Task<DrivingCenterSearchResult> SearchLocalBusinessDataPaged(string query, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Query không được để trống.");
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var offset = (page - 1) * pageSize;
            var searchQuery = BuildDrivingCenterSearchQuery(query);

            string host = _configuration["RapidApi:LocalBusinessDataHost"] ?? "";
            string key = _configuration["RapidApi:LocalBusinessDataKey"] ?? "";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(key))
            {
                throw new Exception("Thiếu cấu hình RapidAPI host hoặc key.");
            }

            string encodedQuery = Uri.EscapeDataString(searchQuery);

            string url = $"https://{host}/search?query={encodedQuery}&limit={pageSize}&offset={offset}";

            Console.WriteLine(
                $"[DrivingCenters] Goi RapidAPI | selected_province={query.Trim()} | search_query={searchQuery} | page={page} | page_size={pageSize} | offset={offset}");

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

            foreach (JsonElement item in dataElement.EnumerateArray().Take(pageSize))
            {
                var center = MapToDrivingCenter(item, searchQuery);
                centers.Add(center);
            }

            Console.WriteLine(
                $"[DrivingCenters] RapidAPI tra ve {centers.Count} DrivingCenter | selected_province={query.Trim()} | centers={string.Join(" | ", centers.Select(c => c.name))}");

            return new DrivingCenterSearchResult
            {
                message = "Tìm kiếm trung tâm từ RapidAPI thành công.",
                total = offset + centers.Count + (centers.Count >= pageSize ? 1 : 0),
                page = page,
                page_size = pageSize,
                has_more = centers.Count >= pageSize,
                data = centers
            };
        }

        private string BuildDrivingCenterSearchQuery(string province)
        {
            var trimmed = province.Trim();
            var normalized = trimmed.ToLowerInvariant();

            if (normalized.Contains("trung tâm")
                || normalized.Contains("trung tam")
                || normalized.Contains("đào tạo")
                || normalized.Contains("dao tao")
                || normalized.Contains("lái xe")
                || normalized.Contains("lai xe"))
            {
                return trimmed;
            }

            return $"trung tâm đào tạo lái xe {trimmed}";
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
                id = BuildStableId(json),
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

        private string BuildStableId(JsonElement json)
        {
            var source = FirstNonEmpty(
                GetString(json, "business_id"),
                GetString(json, "google_id"),
                GetString(json, "place_id"),
                $"{GetString(json, "name")}|{GetString(json, "address")}|{GetString(json, "phone_number")}");

            if (string.IsNullOrWhiteSpace(source))
                source = Guid.NewGuid().ToString("N");

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
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
