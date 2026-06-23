using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class SendOtpEmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Otp { get; set; } = string.Empty;

    public int ExpireMinutes { get; set; } = 5;
}