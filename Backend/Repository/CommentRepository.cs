using Backend.Models;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class CommentRepository
    {
        private readonly FirestoreDb _db;

        public CommentRepository(FirestoreDb db)
        {
            _db = db;
        }

        public async Task<List<Comment>> GetByPostId(string postId)
        {
            var snapshot = await _db.Collection("posts")
                .Document(postId)
                .Collection("comments")
                .WhereEqualTo("isDeleted", false)
                .WhereEqualTo("status", true)
                .OrderBy("createdAt")
                .GetSnapshotAsync();

            var comments = new List<Comment>();

            foreach (var doc in snapshot.Documents)
            {
                var comment = doc.ConvertTo<Comment>();
                comment.commentId = doc.Id;
                comment.postId = postId;
                comments.Add(comment);
            }

            return comments;
        }

        public async Task<Comment> Create(string postId, Comment comment)
        {
            var postRef = _db.Collection("posts").Document(postId);
            var commentRef = postRef.Collection("comments").Document();

            await _db.RunTransactionAsync(async transaction =>
            {
                var postSnapshot = await transaction.GetSnapshotAsync(postRef);

                if (!postSnapshot.Exists)
                {
                    throw new Exception("Post not found");
                }

                comment.commentId = commentRef.Id;
                comment.postId = postId;
                comment.likeCount = 0;
                comment.isDeleted = false;
                comment.status = true;
                comment.createdAt = DateTime.UtcNow;
                comment.updatedAt = DateTime.UtcNow;

                transaction.Set(commentRef, comment);

                transaction.Update(postRef, new Dictionary<string, object>
                {
                    { "commentCount", FieldValue.Increment(1) },
                    { "updatedAt", DateTime.UtcNow }
                });
            });

            return comment;
        }

        public async Task<bool> Delete(
            string postId,
            string commentId,
            string currentUserId,
            bool isAdmin)
        {
            var postRef = _db.Collection("posts").Document(postId);
            var commentRef = postRef.Collection("comments").Document(commentId);

            var deleted = false;

            await _db.RunTransactionAsync(async transaction =>
            {
                var snapshot = await transaction.GetSnapshotAsync(commentRef);

                if (!snapshot.Exists)
                {
                    throw new Exception("Comment not found");
                }

                var comment = snapshot.ConvertTo<Comment>();

                var isOwner = comment.authorId == currentUserId;

                if (!isOwner && !isAdmin)
                {
                    throw new UnauthorizedAccessException(
                        "Không có quyền xóa bình luận"
                    );
                }

                if (comment.isDeleted)
                {
                    deleted = false;
                    return;
                }

                transaction.Update(commentRef, new Dictionary<string, object>
                {
                    { "isDeleted", true },
                    { "status", false },
                    { "updatedAt", DateTime.UtcNow }
                });

                transaction.Update(postRef, new Dictionary<string, object>
                {
                    { "commentCount", FieldValue.Increment(-1) },
                    { "updatedAt", DateTime.UtcNow }
                });

                deleted = true;
            });

            return deleted;
        }
    }
}