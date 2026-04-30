using Google.Cloud.Firestore;

[FirestoreData]
public class User
{
    [FirestoreProperty] public string displayName { get; set; }
    [FirestoreProperty] public string email { get; set; }
    [FirestoreProperty] public string role { get; set; }
    [FirestoreProperty] public string status { get; set; }
    [FirestoreProperty] public string uid { get; set; }
}