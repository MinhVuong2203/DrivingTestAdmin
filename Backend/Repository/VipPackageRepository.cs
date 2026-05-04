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
                .OrderBy("sort_order")
                .OrderBy("vip_price")
                .GetSnapshotAsync();

            return snapshot.Documents.Select(doc =>
            {
                var package = doc.ConvertTo<VipPackage>();
                package.Id = doc.Id;
                return package;
            }).ToList();
        }

        // GET BY ID
        public async Task<VipPackage?> GetByIdAsync(string id)
        {
            var doc = await _db.Collection("vip_packages")
                .Document(id)
                .GetSnapshotAsync();

            if (!doc.Exists) return null;

            var package = doc.ConvertTo<VipPackage>();
            package.Id = doc.Id;
            return package;
        }

        // GET ACTIVE
        public async Task<List<VipPackage>> GetActivePackagesAsync()
        {
            var snapshot = await _db.Collection("vip_packages")
                .WhereEqualTo("is_active", true)
                .OrderBy("sort_order")
                .OrderBy("vip_price")
                .GetSnapshotAsync();

            return snapshot.Documents.Select(doc =>
            {
                var package = doc.ConvertTo<VipPackage>();
                package.Id = doc.Id;
                return package;
            }).ToList();
        }

        // CREATE
        public async Task<VipPackage> CreateAsync(VipPackage package)
        {
            package.CreatedAt = DateTime.UtcNow;
            package.UpdatedAt = DateTime.UtcNow;

            var docRef = await _db.Collection("vip_packages").AddAsync(package);
            package.Id = docRef.Id;

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
                    .SetAsync(package, SetOptions.Overwrite);

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
                (p.Description ?? "").ToLower().Contains(keyword)
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
    }
}