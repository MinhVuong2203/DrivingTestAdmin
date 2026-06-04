using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
                return violations.ToList();

            var searchContext = SearchContext.Create(violations, normalizedKeyword);

            return violations
                .Select(violation => new
                {
                    Violation = violation,
                    Score = GetSearchScore(violation, normalizedKeyword, searchContext)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Violation.FineMin)
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

        private static int GetSearchScore(
            TrafficViolation violation,
            string normalizedKeyword,
            SearchContext searchContext)
        {
            var score = 0;
            var title = NormalizeSearchText(violation.Title);
            var searchText = NormalizeSearchText(violation.SearchText);
            var officialText = GetOfficialSearchText(violation);
            var aliases = violation.Aliases.Select(NormalizeSearchText).ToList();
            var keywords = violation.Keywords.Select(NormalizeSearchText).ToList();
            var searchableBehaviorText = NormalizeSearchText(string.Join(" ", new[]
            {
                violation.Title,
                string.Join(" ", violation.Aliases),
                string.Join(" ", violation.Keywords)
            }));
            var keywordTokens = searchContext.KeywordTokens;
            var intentRule = searchContext.IntentRule;
            var isIntentMatch = intentRule?.IsMatch(title, officialText) == true;

            var exactAliasOrKeywordMatch = aliases.Any(alias => IsPhraseMatch(alias, normalizedKeyword))
                || keywords.Any(keyword => IsPhraseMatch(keyword, normalizedKeyword));
            var isAccidentCrossReference = title.Contains("gay tai nan")
                && exactAliasOrKeywordMatch;
            var isLegalReferenceMatch = title.Contains("gay tai nan")
                && IsLegalReferenceMatch(violation, title, searchContext.LegalReferences);

            if (intentRule != null
                && !isIntentMatch
                && !isAccidentCrossReference
                && !isLegalReferenceMatch)
            {
                return 0;
            }

            if (title.Contains(normalizedKeyword))
                score += 160;

            if (exactAliasOrKeywordMatch)
                score += 140;

            if (aliases.Any(alias => IsPhraseMatch(alias, normalizedKeyword) || IsPhraseMatch(normalizedKeyword, alias)))
                score += 90;

            if (keywords.Any(keyword => IsPhraseMatch(keyword, normalizedKeyword) || IsPhraseMatch(normalizedKeyword, keyword)))
                score += 70;

            if (searchText.Contains(normalizedKeyword))
                score += 35;

            if (intentRule != null)
                score += 120;

            if (isAccidentCrossReference)
                score += 95;

            if (isLegalReferenceMatch)
                score += 105;

            if (keywordTokens.Count > 0 && !isLegalReferenceMatch)
            {
                var titleMatchedTokens = keywordTokens.Count(title.Contains);
                var matchedTokens = keywordTokens.Count(token =>
                    searchableBehaviorText.Contains(token)
                    || searchText.Contains(token));

                var requiredMatches = keywordTokens.Count <= 3
                    ? keywordTokens.Count
                    : Math.Max(3, (int)Math.Ceiling(keywordTokens.Count * 0.75));

                if (!isIntentMatch && matchedTokens < requiredMatches)
                {
                    return 0;
                }

                if (intentRule == null
                    && !title.Contains(normalizedKeyword)
                    && titleMatchedTokens == 0)
                {
                    return 0;
                }

                score += matchedTokens * 12;
                score += titleMatchedTokens * 16;

                if (matchedTokens == keywordTokens.Count)
                    score += 20;
            }

            return score;
        }

        private static bool IsDirectReferenceSource(
            TrafficViolation violation,
            string normalizedKeyword,
            SearchIntentRule? intentRule)
        {
            var title = NormalizeSearchText(violation.Title);
            if (title.Contains("gay tai nan"))
                return false;

            var aliases = violation.Aliases.Select(NormalizeSearchText).ToList();
            var keywords = violation.Keywords.Select(NormalizeSearchText).ToList();
            var exactAliasOrKeywordMatch = aliases.Any(alias => IsPhraseMatch(alias, normalizedKeyword))
                || keywords.Any(keyword => IsPhraseMatch(keyword, normalizedKeyword));

            if (intentRule != null)
                return intentRule.IsMatch(title, GetOfficialSearchText(violation))
                    && (title.Contains(normalizedKeyword) || exactAliasOrKeywordMatch);

            var keywordTokens = GetKeywordTokens(normalizedKeyword);
            if (keywordTokens.Count == 0)
                return false;

            var behaviorText = NormalizeSearchText(string.Join(" ", new[]
            {
                violation.Title,
                string.Join(" ", violation.Aliases),
                string.Join(" ", violation.Keywords)
            }));

            return title.Contains(normalizedKeyword)
                || exactAliasOrKeywordMatch
                || keywordTokens.All(behaviorText.Contains);
        }

        private static bool IsLegalReferenceMatch(
            TrafficViolation violation,
            string normalizedTitle,
            IReadOnlyList<LegalReference> legalReferences)
        {
            if (legalReferences.Count == 0)
                return false;

            var violationArticle = ExtractArticle(violation);

            return legalReferences.Any(reference =>
                violationArticle == reference.Article
                && normalizedTitle.Contains($"khoan {reference.Clause}")
                && (string.IsNullOrWhiteSpace(reference.Point)
                    || normalizedTitle.Contains($"diem {reference.Point}")));
        }

        private static int ExtractArticle(TrafficViolation violation)
        {
            var legalBasis = NormalizeSearchText(violation.PenaltyLegalBasis);
            var match = ArticleRegex.Match(legalBasis);
            if (match.Success && int.TryParse(match.Groups["article"].Value, out var article))
                return article;

            var idMatch = IdArticleRegex.Match(violation.Id);
            return idMatch.Success && int.TryParse(idMatch.Groups["article"].Value, out article)
                ? article
                : 0;
        }

        private static string GetOfficialSearchText(TrafficViolation violation)
        {
            return NormalizeSearchText(string.Join(" ", new[]
            {
                violation.Title,
                violation.SubjectText,
                violation.PenaltyText,
                violation.PenaltyLegalBasis,
                violation.AdditionalPenaltyText,
                violation.AdditionalPenaltyLegalBasis
            }));
        }

        private static List<string> GetKeywordTokens(string normalizedKeyword)
        {
            return normalizedKeyword
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => token.Length >= 2 && !SearchStopWords.Contains(token))
                .Distinct()
                .ToList();
        }

        private static bool IsPhraseMatch(string text, string phrase)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase))
                return false;

            return text.Contains(phrase);
        }

        private static SearchIntentRule? GetIntentRule(string normalizedKeyword, List<string> keywordTokens)
        {
            return SearchIntentRules.FirstOrDefault(rule =>
                rule.QueryPhrases.Any(normalizedKeyword.Contains)
                || (rule.QueryTokens.Count > 0 && rule.QueryTokens.All(keywordTokens.Contains)));
        }

        private sealed class SearchContext
        {
            public IReadOnlyList<string> KeywordTokens { get; init; } = Array.Empty<string>();
            public SearchIntentRule? IntentRule { get; init; }
            public IReadOnlyList<LegalReference> LegalReferences { get; init; } = Array.Empty<LegalReference>();

            public static SearchContext Create(
                IReadOnlyList<TrafficViolation> violations,
                string normalizedKeyword)
            {
                var keywordTokens = GetKeywordTokens(normalizedKeyword);
                var intentRule = GetIntentRule(normalizedKeyword, keywordTokens);
                var legalReferences = violations
                    .Where(violation => IsDirectReferenceSource(violation, normalizedKeyword, intentRule))
                    .Select(ExtractLegalReference)
                    .Where(reference => reference != null)
                    .Select(reference => reference!)
                    .Distinct()
                    .ToList();

                return new SearchContext
                {
                    KeywordTokens = keywordTokens,
                    IntentRule = intentRule,
                    LegalReferences = legalReferences
                };
            }
        }

        private sealed record LegalReference(int Article, int Clause, string Point);

        private static LegalReference? ExtractLegalReference(TrafficViolation violation)
        {
            var legalBasis = NormalizeSearchText(violation.PenaltyLegalBasis);
            var match = LegalReferenceRegex.Match(legalBasis);
            if (!match.Success
                || !int.TryParse(match.Groups["clause"].Value, out var clause)
                || !int.TryParse(match.Groups["article"].Value, out var article))
            {
                return null;
            }

            return new LegalReference(
                article,
                clause,
                match.Groups["point"].Value);
        }

        private sealed class SearchIntentRule
        {
            public IReadOnlyList<string> QueryPhrases { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> QueryTokens { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> RequiredTitlePhrases { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> RequiredTitleAllPhrases { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> RequiredTextPhrases { get; init; } = Array.Empty<string>();

            public bool IsMatch(string normalizedTitle, string normalizedSearchText)
            {
                var hasAllPhrases = RequiredTitleAllPhrases.Count > 0
                    && RequiredTitleAllPhrases.All(normalizedTitle.Contains);
                var hasAnyPhrase = RequiredTitlePhrases.Count > 0
                    && RequiredTitlePhrases.Any(normalizedTitle.Contains);
                var hasTextPhrase = RequiredTextPhrases.Count > 0
                    && RequiredTextPhrases.Any(phrase =>
                        normalizedTitle.Contains(phrase)
                        || normalizedSearchText.Contains(phrase));

                return hasAllPhrases || hasAnyPhrase || hasTextPhrase;
            }
        }

        private static readonly List<SearchIntentRule> SearchIntentRules = new()
        {
            new SearchIntentRule
            {
                QueryPhrases = new[] { "vuot den do", "chay den do", "khong dung den do", "den do" },
                QueryTokens = new[] { "den", "do" },
                RequiredTitlePhrases = new[] { "den do" },
                RequiredTitleAllPhrases = new[] { "khong chap hanh", "den tin hieu" },
                RequiredTextPhrases = new[] { "den do da bat sang" }
            },
            new SearchIntentRule
            {
                QueryPhrases = new[] { "qua toc do", "vuot toc do", "toc do" },
                QueryTokens = new[] { "toc", "do" },
                RequiredTitlePhrases = new[] { "chay qua toc do", "qua toc do quy dinh", "toc do toi da" },
                RequiredTextPhrases = new[] { "chay qua toc do", "qua toc do quy dinh", "toc do toi da", "toc do toi thieu" }
            },
            new SearchIntentRule
            {
                QueryPhrases = new[] { "nong do con", "ruou bia", "chat kich thich" },
                QueryTokens = new[] { "nong", "con" },
                RequiredTitlePhrases = new[] { "nong do con", "chat kich thich" },
                RequiredTextPhrases = new[] { "nong do con", "trong mau hoac hoi tho", "kiem tra ve nong do con", "chat kich thich" }
            },
            new SearchIntentRule
            {
                QueryPhrases = new[] { "mu bao hiem", "khong doi mu", "doi mu" },
                QueryTokens = new[] { "mu", "bao", "hiem" },
                RequiredTitlePhrases = new[] { "mu bao hiem" },
                RequiredTextPhrases = new[] { "mu bao hiem", "doi mu" }
            },
            new SearchIntentRule
            {
                QueryPhrases = new[] { "nguoc chieu", "di nguoc chieu", "chay nguoc chieu" },
                QueryTokens = new[] { "nguoc", "chieu" },
                RequiredTitlePhrases = new[] { "di nguoc chieu cua duong mot chieu", "di nguoc chieu tren duong", "di nguoc chieu duong", "di vao khu vuc cam" },
                RequiredTextPhrases = new[] { "di nguoc chieu", "duong mot chieu", "cam di nguoc chieu", "di vao khu vuc cam" }
            },
            new SearchIntentRule
            {
                QueryPhrases = new[] { "dung xe", "do xe", "cam do", "cam dung" },
                QueryTokens = new[] { "do", "xe" },
                RequiredTitlePhrases = new[] { "dung xe", "do xe", "cam do", "cam dung" },
                RequiredTextPhrases = new[] { "dung xe", "do xe", "cam do", "cam dung", "noi cam dung", "noi cam do" }
            },
            new SearchIntentRule
            {
                QueryPhrases = new[] { "lan tuyen", "lan duong", "sai lan", "vach ke", "vach ke duong" },
                QueryTokens = new[] { "vach", "ke" },
                RequiredTitlePhrases = new[] { "vach ke", "phan duong", "lan duong", "chuyen lan" },
                RequiredTextPhrases = new[] { "vach ke", "phan duong", "lan duong", "chuyen lan", "khong di dung phan duong" }
            },
            new SearchIntentRule
            {
                QueryPhrases = new[] { "bien bao", "khong chap hanh bien bao" },
                QueryTokens = new[] { "bien", "bao" },
                RequiredTitlePhrases = new[] { "bien bao" },
                RequiredTextPhrases = new[] { "bien bao", "bao hieu duong bo", "noi dung cam di vao" }
            },
            new SearchIntentRule
            {
                QueryPhrases = new[] { "chuyen huong", "re trai", "re phai", "quay dau" },
                RequiredTitlePhrases = new[] { "chuyen huong", "re trai", "re phai", "quay dau" },
                RequiredTextPhrases = new[] { "chuyen huong", "re trai", "re phai", "quay dau", "khong co tin hieu bao huong re" }
            },
            new SearchIntentRule
            {
                QueryPhrases = new[] { "khong giay phep", "khong co giay phep", "giay phep lai xe" },
                RequiredTitlePhrases = new[] { "giay phep lai xe", "giay phep" },
                RequiredTextPhrases = new[] { "giay phep lai xe", "khong co giay phep", "khong mang theo giay phep" }
            }
        };

        private static readonly Regex LegalReferenceRegex = new(
            @"diem\s+(?<point>[a-z])\s+khoan\s+(?<clause>\d+)\s+dieu\s+(?<article>\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ArticleRegex = new(
            @"dieu\s+(?<article>\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex IdArticleRegex = new(
            @"dieu(?<article>\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
                .Replace("đ", "d")
                .Replace("Đ", "d")
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
