using Backend.Models;
using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _service;

        public NotificationController(INotificationService service)
        {
            _service = service;
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUserId(string userId)
        {
            var result = await _service.GetByUserId(userId);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Notification notification)
        {
            var result = await _service.Create(notification);
            return Ok(result);
        }

        [HttpPut("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(string notificationId)
        {
            await _service.MarkAsRead(notificationId);
            return Ok(new { message = "Đã đánh dấu đã đọc" });
        }
    }
}