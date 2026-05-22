using Google.Cloud.Firestore;

[FirestoreData]
public class Post
{
    [FirestoreDocumentId] public string postId { get; set; }
    [FirestoreProperty] public string authorId { get; set; }
    [FirestoreProperty] public string authorName { get; set; }
    [FirestoreProperty] public string authorAvatar { get; set; }
    [FirestoreProperty] public string content { get; set; }
    [FirestoreProperty] public string imageUrl { get; set; }
    [FirestoreProperty] public int likeCount { get; set; }
    [FirestoreProperty] public int commentCount { get; set; }
    [FirestoreProperty] public bool isDeleted { get; set; }
    [FirestoreProperty] public bool status { get; set; }
    [FirestoreProperty] public DateTime createdAt { get; set; }
    [FirestoreProperty] public DateTime updatedAt { get; set; }
    [FirestoreProperty] public string address { get; set; } = "";
}
