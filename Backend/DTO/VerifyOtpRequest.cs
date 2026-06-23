using System.ComponentModel.DataAnnotations;

namespace Backend.DTO
{
    public class VerifyOtpRequest
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "OTP không được để trống")]
        [RegularExpression(
            @"^\d{6}$",
            ErrorMessage = "OTP phải gồm 6 chữ số"
        )]
        public string Otp { get; set; } = string.Empty;
    }
}