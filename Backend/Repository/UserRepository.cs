using Backend.DTO;
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
            .Select(ConvertUser)
            .ToList();
    }

    public async Task<UserPageResult> GetPage(UserPageRequest request)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var search = request.Search?.Trim();
        var sortField = NormalizeSortField(request.SortField);
        var sortDescending = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        Query query = _db.Collection("users");

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.WhereEqualTo("status", request.Status.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = query.WhereEqualTo("role", request.Role.Trim().ToLowerInvariant());
        }

        if (request.FromDate.HasValue)
        {
            query = query.WhereGreaterThanOrEqualTo("createdAt", request.FromDate.Value.ToUniversalTime());
        }

        if (request.ToDate.HasValue)
        {
            query = query.WhereLessThanOrEqualTo("createdAt", request.ToDate.Value.ToUniversalTime());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchField = search.Contains('@') ? "email" : "displayName";
            sortField = searchField;
            sortDescending = false;
            query = query
                .WhereGreaterThanOrEqualTo(searchField, search)
                .WhereLessThanOrEqualTo(searchField, search + "\uf8ff");
        }
        else if ((request.FromDate.HasValue || request.ToDate.HasValue) && sortField != "createdAt")
        {
            sortField = "createdAt";
        }

        query = sortDescending
            ? query.OrderByDescending(sortField)
            : query.OrderBy(sortField);

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var cursorSnapshot = await _db.Collection("users")
                .Document(request.Cursor)
                .GetSnapshotAsync();

            if (cursorSnapshot.Exists)
            {
                query = query.StartAfter(cursorSnapshot);
            }
        }

        var snapshot = await query
            .Limit(pageSize + 1)
            .GetSnapshotAsync();

        var hasNextPage = snapshot.Documents.Count > pageSize;
        var pageDocuments = snapshot.Documents.Take(pageSize).ToList();
        var users = pageDocuments
            .Select(ConvertUser)
            .ToList();

        return new UserPageResult
        {
            Items = users,
            PageSize = pageSize,
            NextCursor = hasNextPage && pageDocuments.Count > 0 ? pageDocuments[^1].Id : null,
            HasNextPage = hasNextPage
        };
    }

    // GET BY ID
    public async Task<User?> GetById(string id)
    {
        var doc = await _db.Collection("users").Document(id).GetSnapshotAsync();

        if (!doc.Exists) return null;

        return ConvertUser(doc);
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
    public async Task UpdateStatus(string id, string status, int? lockDays)
    {
        var normalizedStatus = status.Trim().ToLowerInvariant();

        var updates = new Dictionary<string, object>
        {
            { "status", normalizedStatus }
        };

        if (normalizedStatus == "locked")
        {
            updates["unlockAt"] = lockDays is > 0
                ? DateTime.UtcNow.AddDays(lockDays.Value)
                : FieldValue.Delete;
        }
        else if (normalizedStatus == "active")
        {
            updates["unlockAt"] = FieldValue.Delete;
        }

        await _db.Collection("users")
            .Document(id)
            .UpdateAsync(updates);
    }

    public async Task UpdateRole(string id, string role)
    {
        var normalizedRole = role.Trim().ToLowerInvariant();
        var updates = new Dictionary<string, object>
        {
            { "role", normalizedRole }
        };

        if (normalizedRole == "admin")
        {
            updates["isImportant"] = false;
        }
        else
        {
            updates["isImportant"] = FieldValue.Delete;
        }

        await _db.Collection("users")
            .Document(id)
            .UpdateAsync(updates);
    }

    private static string NormalizeSortField(string? sortField)
    {
        return sortField switch
        {
            "displayName" => "displayName",
            "email" => "email",
            "status" => "status",
            "role" => "role",
            _ => "createdAt",
        };
    }

    private static User ConvertUser(DocumentSnapshot document)
    {
        var dict = document.ToDictionary();

        Console.WriteLine($"DOC ID: {document.Id}");

        if (dict.TryGetValue("isImportant", out var rawIsImportant))
        {
            Console.WriteLine($"RAW isImportant = {rawIsImportant}, Type = {rawIsImportant?.GetType()}");
        }
        else
        {
            Console.WriteLine("Field isImportant not found");
        }

        var user = document.ConvertTo<User>();

        Console.WriteLine($"MAPPED isImportant = {user.isImportant}");

        user.uid = document.Id;
        return user;
    }
    
    private static bool TryReadBool(
        IReadOnlyDictionary<string, object> data,
        string key,
        out bool value)
    {
        foreach (var item in data)
        {
            if (!string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.Value is bool boolValue)
            {
                value = boolValue;
                return true;
            }

            if (bool.TryParse(item.Value?.ToString(), out var parsedValue))
            {
                value = parsedValue;
                return true;
            }
        }

        value = false;
        return false;
    }
}
