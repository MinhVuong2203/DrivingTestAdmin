using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Backend.Service
{
    public class TrafficViolationService : ITrafficViolationService
    {
        private readonly TrafficViolationRepository _repository;
        private readonly IWebHostEnvironment _environment;

        public TrafficViolationService(
            TrafficViolationRepository repository,
            IWebHostEnvironment environment)
        {
            _repository = repository;
            _environment = environment;
        }

        public async Task<int> ImportFromJsonAsync()
        {
            var filePath = Path.Combine(
                _environment.ContentRootPath,
                "Data",
                "traffic_violations.json");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Không tìm thấy file Data/traffic_violations.json.", filePath);
            }

            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var violations = JsonSerializer.Deserialize<List<TrafficViolation>>(json)
                ?? new List<TrafficViolation>();

            var validViolations = violations
                .Where(violation => !string.IsNullOrWhiteSpace(violation.Id))
                .Select(NormalizeViolation)
                .ToList();

            return await _repository.ImportAsync(validViolations);
        }

        public async Task<List<TrafficViolation>> SearchAsync(string? keyword, string? vehicleType)
        {
            var violations = await _repository.GetAllActiveAsync();
            var normalizedVehicleType = NormalizeSearchText(vehicleType);
            var normalizedKeyword = NormalizeSearchText(keyword);

            if (!string.IsNullOrWhiteSpace(normalizedVehicleType))
            {
                violations = violations
                    .Where(violation => violation.VehicleTypes
                        .Any(type => NormalizeSearchText(type) == normalizedVehicleType))
                    .ToList();
            }

            if (string.IsNullOrWhiteSpace(normalizedKeyword))
                return violations.Take(50).ToList();

            return violations
                .Select(violation => new
                {
                    Violation = violation,
                    Score = GetSearchScore(violation, normalizedKeyword)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Violation.FineMin)
                .Take(50)
                .Select(item => item.Violation)
                .ToList();
        }

        public async Task<TrafficViolation?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return await _repository.GetByIdAsync(id);
        }

        private static TrafficViolation NormalizeViolation(TrafficViolation violation)
        {
            violation.Id = violation.Id.Trim();
            violation.Status = string.IsNullOrWhiteSpace(violation.Status)
                ? "active"
                : violation.Status.Trim();
            violation.VehicleTypes = CleanList(violation.VehicleTypes);
            violation.Aliases = CleanList(violation.Aliases);
            violation.Keywords = CleanList(violation.Keywords);
            violation.RelatedViolationIds = CleanList(violation.RelatedViolationIds);

            if (string.IsNullOrWhiteSpace(violation.SearchText))
            {
                violation.SearchText = NormalizeSearchText(string.Join(" ", new[]
                {
                    violation.Title,
                    violation.SubjectText,
                    violation.PenaltyText,
                    violation.PenaltyLegalBasis,
                    violation.AdditionalPenaltyText,
                    violation.AdditionalPenaltyLegalBasis,
                    string.Join(" ", violation.Aliases),
                    string.Join(" ", violation.Keywords)
                }));
            }

            return violation;
        }

        private static List<string> CleanList(IEnumerable<string>? values)
        {
            return values?
                .Select(value => value?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .ToList() ?? new List<string>();
        }

        private static int GetSearchScore(TrafficViolation violation, string normalizedKeyword)
        {
            var score = 0;
            var title = NormalizeSearchText(violation.Title);
            var searchText = NormalizeSearchText(violation.SearchText);
            var aliases = violation.Aliases.Select(NormalizeSearchText).ToList();
            var keywords = violation.Keywords.Select(NormalizeSearchText).ToList();

            if (title.Contains(normalizedKeyword))
                score += 100;

            if (aliases.Any(alias => alias.Contains(normalizedKeyword) || normalizedKeyword.Contains(alias)))
                score += 80;

            if (keywords.Any(keyword => keyword.Contains(normalizedKeyword) || normalizedKeyword.Contains(keyword)))
                score += 60;

            if (searchText.Contains(normalizedKeyword))
                score += 40;

            var keywordTokens = normalizedKeyword
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => token.Length >= 3 && !SearchStopWords.Contains(token))
                .Distinct()
                .ToList();

            if (keywordTokens.Count > 0)
            {
                var matchedTokens = keywordTokens.Count(token =>
                    title.Contains(token)
                    || searchText.Contains(token)
                    || aliases.Any(alias => alias.Contains(token))
                    || keywords.Any(keyword => keyword.Contains(token)));

                var requiredMatches = Math.Min(2, keywordTokens.Count);

                if (matchedTokens >= requiredMatches)
                {
                    score += matchedTokens * 10;
                }

                if (matchedTokens == keywordTokens.Count)
                    score += 20;
            }

            return score;
        }

        private static readonly HashSet<string> SearchStopWords = new()
        {
            "doi",
            "voi",
            "nguoi",
            "dieu",
            "khien",
            "phuong",
            "tien",
            "giao",
            "thong",
            "duong",
            "dong",
            "phat",
            "tien",
            "khoan",
            "diem"
        };

        private static string NormalizeSearchText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().ToLowerInvariant()
                .Replace("đ", "d")
                .Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
