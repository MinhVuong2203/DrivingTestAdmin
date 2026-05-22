using Backend.Models;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class VipPackageRepository
    {
        private readonly FirestoreDb _db;

        public VipPackageRepository(FirestoreDb db)
        {
            _db = db;
        }

        // GET ALL
        public async Task<List<VipPackage>> GetAllAsync()
        {
            var snapshot = await _db.Collection("vip_packages")
                .OrderByDescending("updated_at")
                .GetSnapshotAsync();

            return snapshot.Documents.Select(doc =>
            {
                return MapDocument(doc);
            }).ToList();
        }

        // GET BY ID
        public async Task<VipPackage?> GetByIdAsync(string id)
        {
            var doc = await _db.Collection("vip_packages")
                .Document(id)
                .GetSnapshotAsync();

            if (!doc.Exists) return null;

            return MapDocument(doc);
        }

        // GET ACTIVE
        public async Task<List<VipPackage>> GetActivePackagesAsync()
        {
            var packages = await GetAllAsync();

            return packages
                .Where(package => package.IsActive)
                .OrderByDescending(package => package.UpdatedAt)
                .ToList();
        }

        // CREATE
        public async Task<VipPackage> CreateAsync(VipPackage package)
        {
            var docRef = _db.Collection("vip_packages").Document();
            package.Id = docRef.Id;
            package.CreatedAt = DateTime.UtcNow;
            package.UpdatedAt = DateTime.UtcNow;

            await docRef.SetAsync(ToFirestoreData(package), SetOptions.Overwrite);

            return package;
        }

        // UPDATE (overwrite)
        public async Task<bool> UpdateAsync(string id, VipPackage package)
        {
            try
            {
                package.UpdatedAt = DateTime.UtcNow;

                await _db.Collection("vip_packages")
                    .Document(id)
                    .SetAsync(ToFirestoreData(package), SetOptions.Overwrite);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // DELETE
        public async Task<bool> DeleteAsync(string id)
        {
            try
            {
                await _db.Collection("vip_packages")
                    .Document(id)
                    .DeleteAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        // SEARCH (client-side)
        public async Task<List<VipPackage>> SearchAsync(string keyword)
        {
            var all = await GetAllAsync();

            if (string.IsNullOrWhiteSpace(keyword))
                return all;

            keyword = keyword.ToLower();

            return all.Where(p =>
                (p.VipName ?? "").ToLower().Contains(keyword) ||
                p.Descript.Any(description => (description ?? "").ToLower().Contains(keyword))
            ).ToList();
        }

        // UPDATE STATUS (active)
        public async Task<bool> UpdateActiveStatusAsync(string id, bool isActive)
        {
            try
            {
                await _db.Collection("vip_packages")
                    .Document(id)
                    .UpdateAsync(new Dictionary<string, object>
                    {
                        { "is_active", isActive },
                        { "updated_at", DateTime.UtcNow }
                    });

                return true;
            }
            catch
            {
                return false;
            }
        }

        // UPDATE MULTI FIELD
        public async Task<bool> UpdateFieldsAsync(string id, Dictionary<string, object> fields)
        {
            try
            {
                fields["updated_at"] = DateTime.UtcNow;

                await _db.Collection("vip_packages")
                    .Document(id)
                    .UpdateAsync(fields);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, object> ToFirestoreData(VipPackage package)
        {
            var data = new Dictionary<string, object>
            {
                { "id", package.Id ?? string.Empty },
                { "vip_name", package.VipName },
                { "vip_price", package.VipPrice },
                { "isPeriod", package.IsPeriod },
                { "descript", package.Descript },
                { "is_active", package.IsActive },
                { "color_theme", package.ColorTheme },
                { "created_at", package.CreatedAt },
                { "updated_at", package.UpdatedAt }
            };

            if (package.PriceInline.HasValue && package.PriceInline > 0)
            {
                data["price_inline"] = package.PriceInline.Value;
            }

            if (!package.IsPeriod && package.VipTime.HasValue)
            {
                data["vip_time"] = package.VipTime.Value;
            }

            return data;
        }

        private static VipPackage MapDocument(DocumentSnapshot doc)
        {
            var data = doc.ToDictionary();

            return new VipPackage
            {
                Id = doc.Id,
                VipName = GetString(data, "vip_name"),
                VipPrice = GetDouble(data, "vip_price"),
                PriceInline = GetNullableDouble(data, "price_inline"),
                IsPeriod = GetBool(data, "isPeriod"),
                VipTime = GetNullableInt(data, "vip_time"),
                Descript = GetStringList(data, "descript"),
                IsActive = !data.ContainsKey("is_active") || GetBool(data, "is_active"),
                ColorTheme = GetString(data, "color_theme", "blue"),
                CreatedAt = GetDateTime(data, "created_at", DateTime.UtcNow),
                UpdatedAt = GetDateTime(data, "updated_at", DateTime.UtcNow)
            };
        }

        private static string GetString(Dictionary<string, object> data, string key, string defaultValue = "")
        {
            return data.TryGetValue(key, out var value) ? value?.ToString() ?? defaultValue : defaultValue;
        }

        private static double GetDouble(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null) return 0;

            return Convert.ToDouble(value);
        }

        private static double? GetNullableDouble(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null) return null;

            return Convert.ToDouble(value);
        }

        private static int? GetNullableInt(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null) return null;

            return Convert.ToInt32(value);
        }

        private static bool GetBool(Dictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out var value) && value != null && Convert.ToBoolean(value);
        }

        private static DateTime GetDateTime(Dictionary<string, object> data, string key, DateTime defaultValue)
        {
            if (!data.TryGetValue(key, out var value) || value == null) return defaultValue;

            return value switch
            {
                Timestamp timestamp => timestamp.ToDateTime(),
                DateTime dateTime => dateTime,
                _ => defaultValue
            };
        }

        private static List<string> GetStringList(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null) return new List<string>();

            if (value is IEnumerable<object> items)
            {
                return items.Select(item => item?.ToString() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList();
            }

            var legacyValue = value.ToString();
            return string.IsNullOrWhiteSpace(legacyValue) ? new List<string>() : new List<string> { legacyValue };
        }
    }
}
