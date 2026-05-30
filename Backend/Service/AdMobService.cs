using System.Text.Json;
using Backend.Models;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class AdMobService : IAdMobService 
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private static string? _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        public AdMobService(IConfiguration config, HttpClient http)
        {
            _config = config;
            _http = http;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
                return _cachedToken;

            var res = await _http.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _config["AdMob:ClientId"]!,
                    ["client_secret"] = _config["AdMob:ClientSecret"]!,
                    ["refresh_token"] = _config["AdMob:RefreshToken"]!,
                    ["grant_type"] = "refresh_token"
                })
            );

            res.EnsureSuccessStatusCode();
            var data = await res.Content.ReadFromJsonAsync<JsonElement>();
            _cachedToken = data.GetProperty("access_token").GetString()!;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(
                data.GetProperty("expires_in").GetInt32() - 60);

            return _cachedToken;
        }

        public async Task<List<AdMobReportResponse>> GetNetworkReportAsync(
            string startDate, string endDate)
        {
            var token = await GetAccessTokenAsync();
            var accountId = _config["AdMob:AccountId"];

            var body = new
            {
                reportSpec = new
                {
                    dateRange = new
                    {
                        startDate = ParseDate(startDate),
                        endDate = ParseDate(endDate)
                    },
                    dimensions = new[] { "DATE" },
                    metrics = new[]
                    {
                        "ESTIMATED_EARNINGS",
                        "IMPRESSIONS",
                        "CLICKS",
                        "IMPRESSION_RPM"
                    }
                }
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://admob.googleapis.com/v1/accounts/{accountId}/networkReport:generate"
            );
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Content = JsonContent.Create(body);

            var res = await _http.SendAsync(request);
            res.EnsureSuccessStatusCode();

            var raw = await res.Content.ReadAsStringAsync();
            var jsonArray = JsonSerializer.Deserialize<JsonElement[]>(raw);
            var result = new List<AdMobReportResponse>();

            foreach (var item in jsonArray ?? [])
            {
                if (!item.TryGetProperty("row", out var row)) continue;

                var metrics = row.GetProperty("metricValues");
                var dimensions = row.GetProperty("dimensionValues");

                result.Add(new AdMobReportResponse
                {
                    Date = dimensions
                        .GetProperty("DATE")
                        .GetProperty("value")
                        .GetString() ?? "",

                    EstimatedEarnings = double.Parse(
                        metrics.GetProperty("ESTIMATED_EARNINGS")
                               .GetProperty("microsValue")
                               .GetString() ?? "0") / 1_000_000,

                    Impressions = long.Parse(
                        metrics.GetProperty("IMPRESSIONS")
                               .GetProperty("integerValue")
                               .GetString() ?? "0"),

                    Clicks = long.Parse(
                        metrics.GetProperty("CLICKS")
                               .GetProperty("integerValue")
                               .GetString() ?? "0"),

                    Ecpm = double.Parse(
                        metrics.GetProperty("IMPRESSION_RPM")
                               .GetProperty("microsValue")
                               .GetString() ?? "0") / 1_000_000,
                });
            }

            return result;
        }

        private static object ParseDate(string date)
        {
            var p = date.Split('-');
            return new
            {
                year = int.Parse(p[0]),
                month = int.Parse(p[1]),
                day = int.Parse(p[2])
            };
        }
    }
}