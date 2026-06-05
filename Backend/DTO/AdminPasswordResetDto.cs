namespace Backend.DTO
{
    public class AdminPasswordResetOtpRequest
    {
        public string? Email { get; set; }
    }

    public class AdminPasswordResetConfirmRequest
    {
        public string? Email { get; set; }
        public string? Otp { get; set; }
        public string? NewPassword { get; set; }
    }
}
