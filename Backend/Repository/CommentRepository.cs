using Backend.Models;
using Backend.Service.Interface;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class CommentRepository
    {
        private readonly FirestoreDb _db;
        private readonly IModerationService _moderationService;

        public CommentRepository(
            FirestoreDb db,
            IModerationService moderationService)
        {
            _db = db;
            _moderationService = moderationService;
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

            comment.commentId = commentRef.Id;
            comment.postId = postId;
            comment.likeCount = 0;
            comment.isDeleted = false;
            comment.status = true;
            comment.createdAt = DateTime.UtcNow;
            comment.updatedAt = DateTime.UtcNow;

            // 1. Lưu comment trước
            await commentRef.SetAsync(comment);

            // 2. Tăng số lượng bình luận
            await postRef.UpdateAsync(new Dictionary<string, object>
            {
                { "commentCount", FieldValue.Increment(1) },
                { "updatedAt", DateTime.UtcNow }
            });

            // 3. Kiểm duyệt bằng AI Gemini trước, keyword fallback sau
            var violated = await _moderationService.IsPostViolatedByAiFirst(comment.content);

            // 4. Nếu vi phạm thì xóa mềm comment
            if (violated)
            {
                await commentRef.UpdateAsync(new Dictionary<string, object>
                {
                    { "isDeleted", true },
                    { "status", false },
                    { "moderationReason", "AI hoặc keyword phát hiện bình luận vi phạm" },
                    { "moderatedAt", DateTime.UtcNow },
                    { "updatedAt", DateTime.UtcNow }
                });

                await postRef.UpdateAsync(new Dictionary<string, object>
                {
                    { "commentCount", FieldValue.Increment(-1) },
                    { "updatedAt", DateTime.UtcNow }
                });

                comment.isDeleted = true;
                comment.status = false;
            }

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