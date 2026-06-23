namespace Backend.Service.Interface
{
    public interface IEmailService
    {
        Task SendOtpEmail(
            string receiverEmail,
            string otp,
            int expireMinutes,
            CancellationToken cancellationToken = default
        );
    }
}