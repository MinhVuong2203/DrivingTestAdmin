using Backend.Service.Interface;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class PostRepository
    {
        private readonly FirestoreDb _db;
        private readonly IModerationService _moderationService;

        public PostRepository(
        FirestoreDb db,
        IModerationService moderationService)
        {
            _db = db;
            _moderationService = moderationService;
        }

        public async Task<List<Post>> GetAll()
        {
            var snapshot = await _db.Collection("posts").GetSnapshotAsync();

            return snapshot.Documents
                .Select(d => d.ConvertTo<Post>())
                .ToList();
        }

        public async Task<Post?> GetById(string id)
        {
            var doc = await _db.Collection("posts").Document(id).GetSnapshotAsync();

            if (!doc.Exists) return null;

            return doc.ConvertTo<Post>();
        }

        public async Task<List<Post>> GetByAuthorID(string authorId)
        {
            var snapshot = await _db.Collection("posts")
                .WhereEqualTo("authorId", authorId)
                .GetSnapshotAsync();

            return snapshot.Documents
                .Select(d => d.ConvertTo<Post>())
                .ToList();
        }

        public async Task Create(Post post)
        {
            post.isDeleted = false;
            post.status = true;
            post.createdAt = DateTime.UtcNow;
            post.updatedAt = DateTime.UtcNow;

            var postRef = _db.Collection("posts").Document(post.postId);

            // 1. Lưu bài viết trước
            await postRef.SetAsync(post);

            // 2. Kiểm tra sau khi đã đăng
            var violated = await _moderationService.IsPostViolated(post.content);

            // 3. Nếu vi phạm thì đánh dấu đã xóa
            if (violated)
            {
                await postRef.UpdateAsync(new Dictionary<string, object>
        {
            { "isDeleted", true },
            { "status", false },
            { "moderationReason", "Nội dung chứa keyword vi phạm" },
            { "moderatedAt", DateTime.UtcNow },
            { "updatedAt", DateTime.UtcNow }
        });

                post.isDeleted = true;
                post.status = false;
            }
        }

        public async Task Update(string id, Post post)
        {
            await _db.Collection("posts").Document(id).SetAsync(post);
        }

        public async Task Delete(string id)
        {
            var postRef = _db.Collection("posts").Document(id);

            var snapshot = await postRef.GetSnapshotAsync();
            if (!snapshot.Exists)
            {
                throw new Exception("Post not found");
            }

            await postRef.UpdateAsync(new Dictionary<string, object>
            {
                { "isDeleted", true },
                { "status", false },
                { "updatedAt", DateTime.UtcNow }
            });
        }

        public async Task LikePost(string postId, string userId)
        {
            var postRef = _db.Collection("posts").Document(postId);
            var likeRef = postRef.Collection("likes").Document(userId);

            await _db.RunTransactionAsync(async transaction =>
            {
                var postSnap = await transaction.GetSnapshotAsync(postRef);
                if (!postSnap.Exists) throw new Exception("Post not found");

                var likeSnap = await transaction.GetSnapshotAsync(likeRef);
                if (likeSnap.Exists) return;

                transaction.Set(likeRef, new Dictionary<string, object>
                {
                    { "userId", userId },
                    { "createdAt", Timestamp.GetCurrentTimestamp() }
                });

                transaction.Update(postRef, new Dictionary<string, object>
                {
                    { "likeCount", FieldValue.Increment(1) },
                    { "updatedAt", Timestamp.GetCurrentTimestamp() }
                });
            });
        }

        public async Task UnlikePost(string postId, string userId)
        {
            var postRef = _db.Collection("posts").Document(postId);
            var likeRef = postRef.Collection("likes").Document(userId);

            await _db.RunTransactionAsync(async transaction =>
            {
                var postSnap = await transaction.GetSnapshotAsync(postRef);
                if (!postSnap.Exists) throw new Exception("Post not found");

                var likeSnap = await transaction.GetSnapshotAsync(likeRef);
                if (!likeSnap.Exists) return;

                transaction.Delete(likeRef);

                transaction.Update(postRef, new Dictionary<string, object>
                {
                    { "likeCount", FieldValue.Increment(-1) },
                    { "updatedAt", Timestamp.GetCurrentTimestamp() }
                });
            });
        }

        public async Task<bool> IsLiked(string postId, string userId)
        {
            var likeRef = _db
                .Collection("posts")
                .Document(postId)
                .Collection("likes")
                .Document(userId);

            var snap = await likeRef.GetSnapshotAsync();
            return snap.Exists;
        }

        public async Task<List<Post>> GetPostsPaged(int limit, DateTime? lastCreatedAt)
        {
            Query query = _db.Collection("posts")
                .WhereEqualTo("isDeleted", false)
                .OrderByDescending("createdAt")
                .Limit(limit);

            if (lastCreatedAt != null)
            {
                query = _db.Collection("posts")
                    .WhereEqualTo("isDeleted", false)
                    .OrderByDescending("createdAt")
                    .WhereLessThan("createdAt", Timestamp.FromDateTime(lastCreatedAt.Value.ToUniversalTime()))
                    .Limit(limit);
            }

            var snapshot = await query.GetSnapshotAsync();

            return snapshot.Documents
                .Select(doc =>
                {
                    var post = doc.ConvertTo<Post>();
                    post.postId = doc.Id;
                    return post;
                })
                .ToList();
        }
    }
}
