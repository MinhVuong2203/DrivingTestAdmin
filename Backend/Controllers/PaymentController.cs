using System.Text.Json;
using Backend.DTO;
using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IPayOsPaymentService _payOsPaymentService;

        public PaymentController(IPayOsPaymentService payOsPaymentService)
        {
            _payOsPaymentService = payOsPaymentService;
        }

        [HttpPost("payos/create")]
        public async Task<ActionResult<PayOsPaymentResponse>> CreatePayOsPayment([FromBody] CreatePayOsPaymentRequest request)
        {
            try
            {
                var payment = await _payOsPaymentService.CreatePaymentAsync(request);
                return Ok(payment);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Không tạo được link thanh toán PayOS", error = ex.Message });
            }
        }

        [HttpPost("payos-webhook")]
        public async Task<IActionResult> PayOsWebhook([FromBody] JsonElement payload)
        {
            var handled = await _payOsPaymentService.HandleWebhookAsync(payload);
            return handled ? Ok(new { success = true }) : BadRequest(new { success = false });
        }

        [HttpGet("payos-status/{orderCode:long}")]
        public async Task<IActionResult> SyncPayOsStatus(long orderCode)
        {
            var paid = await _payOsPaymentService.SyncPaymentStatusAsync(orderCode);
            return Ok(new { paid });
        }

        [HttpGet("payos-return")]
        public IActionResult PayOsReturn([FromQuery] long? orderCode)
        {
            return Ok(new
            {
                message = "Thanh toán PayOS đã quay về hệ thống. Vui lòng mở lại ứng dụng để kiểm tra VIP.",
                orderCode
            });
        }

        [HttpGet("payos-cancel")]
        public IActionResult PayOsCancel([FromQuery] long? orderCode)
        {
            return Ok(new
            {
                message = "Bạn đã hủy thanh toán PayOS.",
                orderCode
            });
        }
    }
}
