using Google.Cloud.Firestore;
[FirestoreData]
public class VipUser
{
    [FirestoreProperty]
    public string? name { get; set; }

    [FirestoreProperty]
    public string? vipId { get; set; }

    [FirestoreProperty]
    public DateTime? startDate { get; set; }

    [FirestoreProperty]
    public DateTime? endDate { get; set; }
}

[FirestoreData]
public class User
{
    [FirestoreProperty]
    public string uid { get; set; } = "";

    [FirestoreProperty]
    public string? displayName { get; set; }

    [FirestoreProperty]
    public string? email { get; set; }

    [FirestoreProperty]
    public string? role { get; set; }

    [FirestoreProperty]
    public string? status { get; set; }

    [FirestoreProperty]
    public string? photoURL { get; set; }

    [FirestoreProperty]
    public DateTime? createdAt { get; set; }

    [FirestoreProperty]
    public VipUser? vipUser { get; set; }
}
