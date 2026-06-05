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

    public async Task<UserPageResult> GetPage(
        UserPageRequest request,
        string? currentAdminUid,
        bool currentAdminIsImportant)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var search = request.Search?.Trim();
        var sortField = NormalizeSortField(request.SortField);
        var sortDescending = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var snapshot = await _db.Collection("users").GetSnapshotAsync();
        var usersQuery = snapshot.Documents
            .Select(ConvertUser)
            .Where(user => !ShouldHideRootAdmin(user, currentAdminUid, currentAdminIsImportant));

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            usersQuery = usersQuery.Where(user =>
                string.Equals(user.status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var role = request.Role.Trim().ToLowerInvariant();
            usersQuery = usersQuery.Where(user =>
                string.Equals(user.role, role, StringComparison.OrdinalIgnoreCase));
        }

        if (request.FromDate.HasValue)
        {
            var fromDate = request.FromDate.Value.ToUniversalTime();
            usersQuery = usersQuery.Where(user =>
                user.createdAt.HasValue && user.createdAt.Value.ToUniversalTime() >= fromDate);
        }

        if (request.ToDate.HasValue)
        {
            var toDate = request.ToDate.Value.ToUniversalTime();
            usersQuery = usersQuery.Where(user =>
                user.createdAt.HasValue && user.createdAt.Value.ToUniversalTime() <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            usersQuery = usersQuery.Where(user =>
                ContainsIgnoreCase(user.displayName, search) ||
                ContainsIgnoreCase(user.email, search));
        }

        var sortedUsers = SortUsers(usersQuery, sortField, sortDescending).ToList();
        var skip = int.TryParse(request.Cursor, out var parsedCursor) && parsedCursor > 0
            ? parsedCursor
            : 0;
        var pageUsers = sortedUsers
            .Skip(skip)
            .Take(pageSize)
            .ToList();
        var nextOffset = skip + pageUsers.Count;
        var hasNextPage = nextOffset < sortedUsers.Count;

        return new UserPageResult
        {
            Items = pageUsers,
            PageSize = pageSize,
            NextCursor = hasNextPage ? nextOffset.ToString() : null,
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
        var user = document.ConvertTo<User>();
        user.uid = document.Id;
        return user;
    }

    private static bool ShouldHideRootAdmin(
        User user,
        string? currentAdminUid,
        bool currentAdminIsImportant)
    {
        if (user.isImportant)
        {
            return true;
        }

        return currentAdminIsImportant &&
            !string.IsNullOrWhiteSpace(currentAdminUid) &&
            string.Equals(user.uid, currentAdminUid, StringComparison.Ordinal);
    }

    private static bool ContainsIgnoreCase(string? value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<User> SortUsers(
        IEnumerable<User> users,
        string sortField,
        bool sortDescending)
    {
        return sortField switch
        {
            "displayName" => SortByString(users, user => user.displayName, sortDescending),
            "email" => SortByString(users, user => user.email, sortDescending),
            "status" => SortByString(users, user => user.status, sortDescending),
            "role" => SortByString(users, user => user.role, sortDescending),
            _ => SortByDate(users, user => user.createdAt, sortDescending),
        };
    }

    private static IEnumerable<User> SortByString(
        IEnumerable<User> users,
        Func<User, string?> selector,
        bool descending)
    {
        return descending
            ? users
                .OrderBy(user => string.IsNullOrWhiteSpace(selector(user)))
                .ThenByDescending(user => selector(user), StringComparer.OrdinalIgnoreCase)
                .ThenBy(user => user.uid, StringComparer.Ordinal)
            : users
                .OrderBy(user => string.IsNullOrWhiteSpace(selector(user)))
                .ThenBy(user => selector(user), StringComparer.OrdinalIgnoreCase)
                .ThenBy(user => user.uid, StringComparer.Ordinal);
    }

    private static IEnumerable<User> SortByDate(
        IEnumerable<User> users,
        Func<User, DateTime?> selector,
        bool descending)
    {
        return descending
            ? users
                .OrderBy(user => !selector(user).HasValue)
                .ThenByDescending(user => selector(user))
                .ThenBy(user => user.uid, StringComparer.Ordinal)
            : users
                .OrderBy(user => !selector(user).HasValue)
                .ThenBy(user => selector(user))
                .ThenBy(user => user.uid, StringComparer.Ordinal);
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
