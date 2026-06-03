using Google.Cloud.Firestore;

namespace Backend.Models
{
    [FirestoreData]
    public class Comment
    {
        [FirestoreDocumentId]
        public string commentId { get; set; } = "";

        [FirestoreProperty]
        public string postId { get; set; } = "";

        [FirestoreProperty]
        public string authorId { get; set; } = "";

        [FirestoreProperty]
        public string authorName { get; set; } = "";

        [FirestoreProperty]
        public string authorAvatar { get; set; } = "";

        [FirestoreProperty]
        public string content { get; set; } = "";

        [FirestoreProperty]
        public int likeCount { get; set; }

        [FirestoreProperty]
        public bool isDeleted { get; set; }

        [FirestoreProperty]
        public bool status { get; set; }

        [FirestoreProperty]
        public DateTime createdAt { get; set; }

        [FirestoreProperty]
        public DateTime updatedAt { get; set; }
        [FirestoreProperty] public string moderationReason { get; set; } = "";
        [FirestoreProperty] public DateTime? moderatedAt { get; set; }
        [FirestoreProperty]
        public string parentCommentId { get; set; } = "";

        [FirestoreProperty]
        public string replyToUserId { get; set; } = "";

        [FirestoreProperty]
        public string replyToUserName { get; set; } = "";
    }
}
