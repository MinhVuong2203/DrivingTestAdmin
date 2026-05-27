using Microsoft.AspNetCore.Http;

namespace Backend.Service.Interface
{
    public interface IUserAuthService
    {
        Task<UserAuthResult> AuthenticateAsync(HttpContext httpContext, CancellationToken cancellationToken);
    }

    public sealed record UserAuthResult(
        bool Succeeded,
        int StatusCode,
        string Message,
        string? Uid = null
    );
}
