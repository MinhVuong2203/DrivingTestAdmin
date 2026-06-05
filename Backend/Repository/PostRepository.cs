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

        private async Task ApplyVipInfo(Post post)
        {
            if (string.IsNullOrWhiteSpace(post.authorId))
            {
                post.authorIsVip = false;
                post.authorVipName = "";
                return;
            }

            var userSnap = await _db.Collection("users")
                .Document(post.authorId)
                .GetSnapshotAsync();

            if (!userSnap.Exists)
            {
                post.authorIsVip = false;
                post.authorVipName = "";
                return;
            }

            var data = userSnap.ToDictionary();

            if (!data.ContainsKey("vipUser") ||
                data["vipUser"] is not Dictionary<string, object> vipUser)
            {
                post.authorIsVip = false;
                post.authorVipName = "";
                return;
            }

            var hasVipId = vipUser.ContainsKey("vipId") &&
                           !string.IsNullOrWhiteSpace(vipUser["vipId"]?.ToString());

            var stillActive = true;

            if (vipUser.ContainsKey("endDate") &&
                vipUser["endDate"] is Google.Cloud.Firestore.Timestamp endTimestamp)
            {
                stillActive = endTimestamp.ToDateTime() > DateTime.UtcNow;
            }

            post.authorIsVip = hasVipId && stillActive;

            post.authorVipName = post.authorIsVip
                ? vipUser.ContainsKey("name")
                    ? vipUser["name"]?.ToString() ?? "VIP"
                    : "VIP"
                : "";
        }

        public async Task<List<Post>> GetAll()
        {
            var snapshot = await _db.Collection("posts")
                .OrderByDescending("createdAt")
                .GetSnapshotAsync();

            var posts = snapshot.Documents
                .Select(doc =>
                {
                    var post = doc.ConvertTo<Post>();
                    post.postId = doc.Id;
                    return post;
                })
                .ToList();

            foreach (var post in posts)
            {
                await ApplyVipInfo(post);
            }

            return posts;
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

        public async Task<Post> Create(Post post)
        {
            post.isDeleted = false;
            post.status = true;
            post.createdAt = DateTime.UtcNow;
            post.updatedAt = DateTime.UtcNow;

            await ApplyVipInfo(post);

            var postRef = _db.Collection("posts").Document(post.postId);

            await postRef.SetAsync(post);

            _ = Task.Run(async () =>
            {
                try
                {
                    var violated = await _moderationService.IsPostViolatedByAiFirst(post.content);

                    if (violated)
                    {
                        await postRef.UpdateAsync(new Dictionary<string, object>
                {
                    { "isDeleted", true },
                    { "status", false },
                    { "moderationReason", "AI hoặc keyword phát hiện nội dung vi phạm" },
                    { "moderatedAt", DateTime.UtcNow },
                    { "updatedAt", DateTime.UtcNow }
                });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Moderation error: {ex.Message}");
                }
            });

            return post;
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
                    .WhereLessThan(
                        "createdAt",
                        Timestamp.FromDateTime(lastCreatedAt.Value.ToUniversalTime())
                    )
                    .Limit(limit);
            }

            var snapshot = await query.GetSnapshotAsync();

            var posts = snapshot.Documents
                .Select(doc =>
                {
                    var post = doc.ConvertTo<Post>();
                    post.postId = doc.Id;
                    return post;
                })
                .ToList();

            foreach (var post in posts)
            {
                await ApplyVipInfo(post);
            }

            return posts;
        }
    }
}
