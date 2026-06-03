using Backend.Models;
using Backend.Service;
using Backend.Service.Interface;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class CommentRepository
    {
        private readonly FirestoreDb _db;
        private readonly IModerationService _moderationService;
        private readonly INotificationPushService _pushService;
        private readonly INotificationService _notificationService;

        public CommentRepository(
            FirestoreDb db,
            IModerationService moderationService,
            INotificationPushService pushService,
            INotificationService notificationService)
        {
            _db = db;
            _moderationService = moderationService;
            _pushService = pushService;
            _notificationService = notificationService;
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

            if (!comment.isDeleted && comment.status)
            {
                var postSnap = await postRef.GetSnapshotAsync();

                if (postSnap.Exists)
                {
                    var post = postSnap.ConvertTo<Post>();

                    // 1. Người khác comment vào bài viết của chủ bài
                    if (string.IsNullOrWhiteSpace(comment.parentCommentId))
                    {
                        if (post.authorId != comment.authorId)
                        {
                            await _notificationService.Create(
                                new Notification
                                {
                                    userId = post.authorId,
                                    title = "Có bình luận mới",
                                    message = $"{comment.authorName} đã bình luận bài viết của bạn",
                                    type = "post_comment",
                                    postId = postId,
                                    isRead = false,
                                    createdAt = DateTime.UtcNow
                                });

                            await _pushService.SendPushToUser(
                                post.authorId,
                                "Có bình luận mới",
                                $"{comment.authorName} đã bình luận bài viết của bạn",
                                postId,
                                "post_comment"
                            );
                        }
                    }

                    // 2. Người khác reply comment
                    if (!string.IsNullOrWhiteSpace(comment.parentCommentId)
                        && !string.IsNullOrWhiteSpace(comment.replyToUserId))
                    {
                        if (comment.replyToUserId != comment.authorId)
                        {
                            await _notificationService.Create(
                                new Notification
                                {
                                    userId = comment.replyToUserId,
                                    title = "Có phản hồi mới",
                                    message = $"{comment.authorName} đã phản hồi bình luận của bạn",
                                    type = "comment_reply",
                                    postId = postId,
                                    isRead = false,
                                    createdAt = DateTime.UtcNow
                                });

                            await _pushService.SendPushToUser(
                                comment.replyToUserId,
                                "Có phản hồi mới",
                                $"{comment.authorName} đã phản hồi bình luận của bạn",
                                postId,
                                "comment_reply"
                            );
                        }
                    }
                }
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