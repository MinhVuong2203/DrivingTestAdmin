using Backend.DTO;

namespace Backend.Service.Interface
{
    public interface IAdminPasswordResetService
    {
        Task RequestOtpAsync(AdminPasswordResetOtpRequest request, CancellationToken cancellationToken);

        Task<AdminPasswordResetResult> ResetPasswordAsync(
            AdminPasswordResetConfirmRequest request,
            CancellationToken cancellationToken);
    }

    public sealed record AdminPasswordResetResult(bool Succeeded, string Message);
}
