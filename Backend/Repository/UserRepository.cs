using Google.Cloud.Firestore;

public class UserRepository
{
    private readonly FirestoreDb _db;

    public UserRepository(FirestoreDb db)
    {
        _db = db;
    }

    // GET ALL
    public async Task<List<User>> GetAll()
    {
        var snapshot = await _db.Collection("users").GetSnapshotAsync();

        return snapshot.Documents
            .Select(d => {
                var user = d.ConvertTo<User>();
                user.uid = d.Id;
                return user;
            })
            .ToList();
    }

    // GET BY ID
    public async Task<User?> GetById(string id)
    {
        var doc = await _db.Collection("users").Document(id).GetSnapshotAsync();

        if (!doc.Exists) return null;

        return doc.ConvertTo<User>();
    }

    // CREATE
    public async Task Create(User user)
    {
        await _db.Collection("users").Document(user.uid).SetAsync(user);
    }

    // UPDATE
    public async Task Update(string id, User user)
    {
        await _db.Collection("users").Document(id).SetAsync(user);
    }

    // DELETE
    public async Task Delete(string id)
    {
        await _db.Collection("users").Document(id).DeleteAsync();
    }

    // lock/unlock
    public async Task UpdateStatus(string id, string status)
    {
        await _db.Collection("users")
            .Document(id)
            .UpdateAsync("status", status);
    }
}