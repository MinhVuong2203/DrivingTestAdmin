using Microsoft.AspNetCore.Http;

namespace Backend.Service.Interface
{
    public interface IAdminAuthService
    {
        Task<AdminAuthResult> AuthorizeAsync(HttpContext httpContext, CancellationToken cancellationToken);
    }

    public sealed record AdminAuthResult(
        bool Succeeded,
        int StatusCode,
        string Message,
        string? Uid = null
    );
}
