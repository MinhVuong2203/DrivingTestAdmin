namespace Backend.Service.Interface
{
    public interface IOtpService
    {
        Task SendOtp(
            string email,
            CancellationToken cancellationToken = default
        );

        bool VerifyOtp(
            string email,
            string otp,
            out string message
        );
    }
}