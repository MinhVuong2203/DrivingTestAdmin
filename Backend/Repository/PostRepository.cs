using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class PostRepository
    {
        private readonly FirestoreDb _db;

        public PostRepository(FirestoreDb db)
        {
            _db = db;
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
            await _db.Collection("posts").Document(post.postId).SetAsync(post);
        }

        public async Task Update(string id, Post post)
        {
            await _db.Collection("posts").Document(id).SetAsync(post);
        }

        public async Task Delete(string id)
        {
            await _db.Collection("posts").Document(id).DeleteAsync();
        }

        public async Task LikePost(string postId, string userId)
        {
            var postRef = _db.Collection("posts").Document(postId);
            var likeRef = postRef.Collection("likes").Document(userId);

            var likeSnap = await likeRef.GetSnapshotAsync();
            if (likeSnap.Exists) return; // đã like thì bỏ qua

            await likeRef.SetAsync(new { userId, createdAt = Timestamp.GetCurrentTimestamp() });
            await postRef.UpdateAsync("likeCount", FieldValue.Increment(1));
            await postRef.UpdateAsync("updatedAt", Timestamp.GetCurrentTimestamp());
        }

        public async Task UnlikePost(string postId, string userId)
        {
            var postRef = _db.Collection("posts").Document(postId);
            var likeRef = postRef.Collection("likes").Document(userId);

            var likeSnap = await likeRef.GetSnapshotAsync();
            if (!likeSnap.Exists) return; // chưa like thì bỏ qua

            await likeRef.DeleteAsync();
            await postRef.UpdateAsync("likeCount", FieldValue.Increment(-1));
            await postRef.UpdateAsync("updatedAt", Timestamp.GetCurrentTimestamp());
        }
    }
}
