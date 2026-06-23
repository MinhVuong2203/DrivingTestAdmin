using Backend.DTO;
using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        private readonly IOtpService _otpService;

        public EmailController(
            IOtpService otpService
        )
        {
            _otpService = otpService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendOtp(
            [FromBody] SendOtpRequest request,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await _otpService.SendOtp(
                    request.Email,
                    cancellationToken
                );

                return Ok(new
                {
                    success = true,
                    message = "Mã OTP đã được gửi đến email",
                    expireMinutes = 5
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "Không thể gửi OTP",
                        detail = ex.Message
                    }
                );
            }
        }

        [HttpPost("verify")]
        public IActionResult VerifyOtp(
            [FromBody] VerifyOtpRequest request
        )
        {
            var success = _otpService.VerifyOtp(
                request.Email,
                request.Otp,
                out var message
            );

            if (!success)
            {
                return BadRequest(new
                {
                    success = false,
                    message
                });
            }

            return Ok(new
            {
                success = true,
                message
            });
        }
    }
}