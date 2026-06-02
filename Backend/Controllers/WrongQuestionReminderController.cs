using Backend.Filters;
using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/wrong-question-reminders")]
    [ApiController]
    [AdminAuthorize]
    public class WrongQuestionReminderController : ControllerBase
    {
        private readonly IWrongQuestionReminderService _service;

        public WrongQuestionReminderController(
            IWrongQuestionReminderService service)
        {
            _service = service;
        }

        [HttpPost("send-now")]
        public async Task<IActionResult> SendNow(CancellationToken cancellationToken)
        {
            var result = await _service.SendToEligibleUsersAsync(cancellationToken);
            return Ok(result);
        }
    }
}
