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
    public bool isImportant { get; set; } = false;

    [FirestoreProperty]
    public string? status { get; set; }

    [FirestoreProperty]
    public DateTime? unlockAt { get; set; }

    [FirestoreProperty]
    public string? photoURL { get; set; }

    [FirestoreProperty]
    public DateTime? createdAt { get; set; }

    [FirestoreProperty]
    public VipUser? vipUser { get; set; }

    [FirestoreProperty]
    public bool reminder_wrong { get; set; } = false;

    [FirestoreProperty]
    public bool wrong_reminder_enabled { get; set; } = true;

    [FirestoreProperty]
    public List<string>? fcm_tokens { get; set; }
}
