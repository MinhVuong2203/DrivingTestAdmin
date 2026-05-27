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

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return new AdminAuthResult(
                    false,
                    StatusCodes.Status403Forbidden,
                    "Không có quyền",
                    userAuth.Uid
                );
            }

            httpContext.Items["AdminUid"] = userAuth.Uid;

            return new AdminAuthResult(
                true,
                StatusCodes.Status200OK,
                "OK",
                userAuth.Uid
            );
        }
    }
}
