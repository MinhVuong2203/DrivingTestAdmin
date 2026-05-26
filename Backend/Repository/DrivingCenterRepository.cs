using Backend.DTO;
using Backend.Models;
using Google.Cloud.Firestore;
using System.Globalization;
using System.Text;

namespace Backend.Repository
{
    public class DrivingCenterRepository
    {
        private readonly FirestoreDb _db;

        public DrivingCenterRepository(FirestoreDb db)
        {
            _db = db;
        }

        public async Task<List<DrivingCenter>> GetAll()
        {
            var snapshot = await _db.Collection("driving_centers")
                .OrderByDescending("rating")
                .GetSnapshotAsync();

            return snapshot.Documents
                .Select(d =>
                {
                    var center = d.ConvertTo<DrivingCenter>();
                    center.id = d.Id;
                    return center;
                })
                .ToList();
        }

        public async Task<List<DrivingCenter>> Search(string? keyword)
        {
            var centers = await GetAll();
            var normalizedKeyword = NormalizeSearchText(keyword);

            if (string.IsNullOrWhiteSpace(normalizedKeyword))
                return centers;

            return centers
                .Where(c => MatchesKeyword(c, normalizedKeyword))
                .ToList();
        }

        public async Task<DrivingCenterSearchResult> SearchPaged(string? keyword, int page, int pageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var cityResult = await TrySearchByCityPaged(keyword, page, pageSize);
            if (cityResult != null)
                return cityResult;

            var centers = await GetAll();
            var normalizedKeyword = NormalizeSearchText(keyword);

            if (!string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                centers = centers
                    .Where(c => MatchesKeyword(c, normalizedKeyword))
                    .ToList();
            }

            var total = centers.Count;
            var skip = (page - 1) * pageSize;
            var data = centers
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            return new DrivingCenterSearchResult
            {
                message = "Tìm kiếm trung tâm thành công.",
                total = total,
                page = page,
                page_size = pageSize,
                has_more = skip + data.Count < total,
                data = data
            };
        }

        private async Task<DrivingCenterSearchResult?> TrySearchByCityPaged(string? keyword, int page, int pageSize)
        {
            var cityKeyword = ToFirestoreCityKeyword(keyword);
            if (string.IsNullOrWhiteSpace(cityKeyword))
                return null;

            var skip = (page - 1) * pageSize;

            try
            {
                var snapshot = await _db.Collection("driving_centers")
                    .WhereEqualTo("city", cityKeyword)
                    .OrderByDescending("rating")
                    .Offset(skip)
                    .Limit(pageSize + 1)
                    .GetSnapshotAsync();

                if (page == 1 && snapshot.Documents.Count == 0)
                    return null;

                var data = snapshot.Documents
                    .Take(pageSize)
                    .Select(d =>
                    {
                        var center = d.ConvertTo<DrivingCenter>();
                        center.id = d.Id;
                        return center;
                    })
                    .ToList();

                var hasMore = snapshot.Documents.Count > pageSize;

                return new DrivingCenterSearchResult
                {
                    message = "Tìm kiếm trung tâm thành công.",
                    total = skip + data.Count + (hasMore ? 1 : 0),
                    page = page,
                    page_size = pageSize,
                    has_more = hasMore,
                    data = data
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<DrivingCenter?> GetById(string id)
        {
            var docRef = _db.Collection("driving_centers").Document(id);
            var snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
                return null;

            var center = snapshot.ConvertTo<DrivingCenter>();
            center.id = snapshot.Id;

            return center;
        }

        public async Task<string> Add(DrivingCenter center)
        {
            center.created_at = DateTime.UtcNow;
            center.updated_at = DateTime.UtcNow;

            var docRef = await _db.Collection("driving_centers").AddAsync(center);
            return docRef.Id;
        }

        public async Task<int> AddMany(List<DrivingCenter> centers)
        {
            if (centers == null || centers.Count == 0)
                return 0;

            int savedCount = 0;

            foreach (var center in centers)
            {
                await Add(center);
                savedCount++;
            }

            return savedCount;
        }

        private static bool MatchesKeyword(DrivingCenter center, string normalizedKeyword)
        {
            return NormalizeSearchText(center.name).Contains(normalizedKeyword)
                || NormalizeSearchText(center.address).Contains(normalizedKeyword)
                || NormalizeSearchText(center.district).Contains(normalizedKeyword)
                || NormalizeSearchText(center.city).Contains(normalizedKeyword);
        }

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

        private static string ToFirestoreCityKeyword(string? value)
        {
            var normalized = NormalizeSearchText(value);
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            var words = normalized
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..]);

            return string.Join(" ", words);
        }
    }
}
