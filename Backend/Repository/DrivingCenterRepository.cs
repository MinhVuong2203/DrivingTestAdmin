using Backend.Models;
using Google.Cloud.Firestore;
using System.Collections;

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
            var snapshot = await _db.Collection("driving_centers").OrderByDescending("rating").GetSnapshotAsync();

            return snapshot.Documents
                .Select(d =>
                {
                    var center = d.ConvertTo<DrivingCenter>();
                    center.id = d.Id;
                    return center;
                })
                .ToList();
        }

        // Tìm kiếm theo tên, địa chỉ, quận/huyện, thành phố
        public async Task<List<DrivingCenter>> Search(string? keyword)
        {
            var centers = await GetAll();

            if (string.IsNullOrWhiteSpace(keyword))
                return centers;

            keyword = keyword.Trim().ToLower();

            return centers
                .Where(c =>
                    (c.name ?? "").ToLower().Contains(keyword) ||
                    (c.address ?? "").ToLower().Contains(keyword) ||                
                    (c.district ?? "").ToLower().Contains(keyword) ||
                    (c.city ?? "").ToLower().Contains(keyword)
                )
                .ToList();
        }

        // Lấy chi tiết theo document id
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
    }
}
