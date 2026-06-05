using Backend.Service.Interface;
using Google.Cloud.Firestore;

namespace Backend.Service
{
    public class AdminAuthService : IAdminAuthService
    {
        private readonly IUserAuthService _userAuthService;
        private readonly FirestoreDb _db;

        public AdminAuthService(IUserAuthService userAuthService, FirestoreDb db)
        {
            _userAuthService = userAuthService;
            _db = db;
        }

        public async Task<AdminAuthResult> AuthorizeAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken)
        {
            var userAuth = await _userAuthService.AuthenticateAsync(httpContext, cancellationToken);

            if (!userAuth.Succeeded || string.IsNullOrWhiteSpace(userAuth.Uid))
            {
                return new AdminAuthResult(
                    false,
                    userAuth.StatusCode,
                    userAuth.Message,
                    userAuth.Uid
                );
            }

            var userSnapshot = await _db
                .Collection("users")
                .Document(userAuth.Uid)
                .GetSnapshotAsync(cancellationToken);

            if (!userSnapshot.Exists)
            {
                return new AdminAuthResult(
                    false,
                    StatusCodes.Status403Forbidden,
                    "Không có quyền",
                    userAuth.Uid
                );
            }

            var user = userSnapshot.ToDictionary();
            var role = user.TryGetValue("role", out var roleValue)
                ? roleValue?.ToString()
                : null;
            var status = user.TryGetValue("status", out var statusValue)
                ? statusValue?.ToString()
                : "active";
            var isImportant = TryReadBool(user, "isImportant");

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return new AdminAuthResult(
                    false,
                    StatusCodes.Status403Forbidden,
                    "Không có quyền",
                    userAuth.Uid
                );
            }

            if (!string.Equals(status ?? "active", "active", StringComparison.OrdinalIgnoreCase))
            {
                return new AdminAuthResult(
                    false,
                    StatusCodes.Status403Forbidden,
                    "Tai khoan quan tri dang bi khoa.",
                    userAuth.Uid
                );
            }

            httpContext.Items["AdminUid"] = userAuth.Uid;
            httpContext.Items["AdminIsImportant"] = isImportant;
            httpContext.Items["AdminStatus"] = status ?? "active";

            return new AdminAuthResult(
                true,
                StatusCodes.Status200OK,
                "OK",
                userAuth.Uid
            );
        }

        private static bool TryReadBool(IReadOnlyDictionary<string, object> data, string key)
        {
            foreach (var item in data)
            {
                if (!string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (item.Value is bool boolValue)
                {
                    return boolValue;
                }

                if (bool.TryParse(item.Value?.ToString(), out var parsedValue))
                {
                    return parsedValue;
                }
            }

            return false;
        }
    }
}
