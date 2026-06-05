using Backend.DTO;
using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/admin/password-reset")]
    [ApiController]
    public class AdminPasswordResetController : ControllerBase
    {
        private readonly IAdminPasswordResetService _passwordResetService;

        public AdminPasswordResetController(IAdminPasswordResetService passwordResetService)
        {
            _passwordResetService = passwordResetService;
        }

        [HttpPost("request-otp")]
        public async Task<IActionResult> RequestOtp(
            [FromBody] AdminPasswordResetOtpRequest request,
            CancellationToken cancellationToken)
        {
            await _passwordResetService.RequestOtpAsync(request, cancellationToken);

            return Ok(new
            {
                message = "Nếu email thuộc tài khoản admin đang hoạt động, mã OTP sẽ được gửi đến Gmail của bạn."
            });
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm(
            [FromBody] AdminPasswordResetConfirmRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _passwordResetService.ResetPasswordAsync(request, cancellationToken);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message });
        }
    }
}
