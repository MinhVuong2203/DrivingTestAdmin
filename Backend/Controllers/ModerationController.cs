using Backend.Models;
using Backend.Service;
using Backend.Service.Interface;
using Backend.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AdminAuthorize]
    public class ModerationController : ControllerBase
    {
        private readonly IModerationService _moderationService;
        private readonly IAiModerationService _aiModerationService;

        public ModerationController(
        IModerationService moderationService,
        IAiModerationService aiModerationService)
        {
            _moderationService = moderationService;
            _aiModerationService = aiModerationService;
        }

        // GET: api/Moderation/keywords
        [HttpGet("keywords")]
        public async Task<IActionResult> GetKeywords()
        {
            try
            {
                var result = await _moderationService.GetAllKeywords();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/Moderation/keywords
        [HttpPost("keywords")]
        public async Task<IActionResult> CreateKeyword([FromBody] ModerationKeyword keyword)
        {
            try
            {
                var result = await _moderationService.CreateKeyword(keyword);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: api/Moderation/keywords/{id}/toggle
        [HttpPut("keywords/{id}/toggle")]
        public async Task<IActionResult> ToggleKeyword(
            string id,
            [FromQuery] bool isActive)
        {
            try
            {
                await _moderationService.ToggleKeyword(id, isActive);
                return Ok(new
                {
                    message = "Cập nhật trạng thái keyword thành công"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/Moderation/auto-delete
        [HttpGet("auto-delete")]
        public async Task<IActionResult> GetAutoDeleteStatus()
        {
            try
            {
                var enabled = await _moderationService.IsAutoDeleteEnabled();

                return Ok(new
                {
                    enabled
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: api/Moderation/auto-delete
        [HttpPut("auto-delete")]
        public async Task<IActionResult> SetAutoDelete([FromQuery] bool enabled)
        {
            try
            {
                await _moderationService.SetAutoDeleteEnabled(enabled);

                return Ok(new
                {
                    message = enabled
                        ? "Đã bật tự động xóa bài vi phạm"
                        : "Đã tắt tự động xóa bài vi phạm"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/Moderation/check
        [HttpPost("check")]
        public async Task<IActionResult> CheckContent([FromBody] string content)
        {
            try
            {
                var violated = await _moderationService.IsPostViolated(content);

                return Ok(new
                {
                    violated
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("check-ai")]
        public async Task<IActionResult> CheckAi([FromBody] string content)
        {
            var result = await _aiModerationService.CheckPostByAi(content);

            return Ok(result);
        }
    }
}
