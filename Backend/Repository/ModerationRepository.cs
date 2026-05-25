using Backend.Models;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class ModerationRepository
    {
        private readonly FirestoreDb _db;

        public ModerationRepository(FirestoreDb db)
        {
            _db = db;
        }

        public async Task<bool> IsAutoDeleteEnabled()
        {
            var doc = await _db.Collection("moderation_settings")
                .Document("post_auto_delete")
                .GetSnapshotAsync();

            if (!doc.Exists) return false;

            return doc.ContainsField("enabled") &&
                   doc.GetValue<bool>("enabled");
        }

        public async Task SetAutoDeleteEnabled(bool enabled)
        {
            await _db.Collection("moderation_settings")
                .Document("post_auto_delete")
                .SetAsync(new Dictionary<string, object>
                {
                    { "enabled", enabled },
                    { "updatedAt", DateTime.UtcNow }
                }, SetOptions.MergeAll);
        }

        public async Task<List<ModerationKeyword>> GetActiveKeywords()
        {
            var snapshot = await _db.Collection("moderation_keywords")
                .WhereEqualTo("isActive", true)
                .GetSnapshotAsync();

            return snapshot.Documents.Select(doc =>
            {
                var item = doc.ConvertTo<ModerationKeyword>();
                item.keywordId = doc.Id;
                return item;
            }).ToList();
        }

        public async Task<List<ModerationKeyword>> GetAllKeywords()
        {
            var snapshot = await _db.Collection("moderation_keywords")
                .GetSnapshotAsync();

            return snapshot.Documents.Select(doc =>
            {
                var item = doc.ConvertTo<ModerationKeyword>();
                item.keywordId = doc.Id;
                return item;
            }).ToList();
        }

        public async Task<ModerationKeyword> CreateKeyword(ModerationKeyword keyword)
        {
            var docRef = _db.Collection("moderation_keywords").Document();

            keyword.keywordId = docRef.Id;
            keyword.keyword = keyword.keyword.Trim().ToLower();
            keyword.isActive = true;
            keyword.createdAt = DateTime.UtcNow;
            keyword.updatedAt = DateTime.UtcNow;

            await docRef.SetAsync(keyword);

            return keyword;
        }

        public async Task ToggleKeyword(string keywordId, bool isActive)
        {
            await _db.Collection("moderation_keywords")
                .Document(keywordId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "isActive", isActive },
                    { "updatedAt", DateTime.UtcNow }
                });
        }
    }
}