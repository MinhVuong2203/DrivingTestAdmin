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
    }
}
