using Backend.DTO;
using Backend.Filters;
using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [UserAuthorize]
    public class TrafficSignRecognitionController : ControllerBase
    {
        private readonly ITrafficSignRecognitionService _recognitionService;

        public TrafficSignRecognitionController(
            ITrafficSignRecognitionService recognitionService)
        {
            _recognitionService = recognitionService;
        }

        [HttpPost("recognize")]
        public async Task<IActionResult> Recognize(
            [FromBody] RecognizeTrafficSignRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Base64Image))
            {
                return BadRequest(new
                {
                    message = "Base64Image is required."
                });
            }

            var result = await _recognitionService.RecognizeTrafficSign(
                request.Base64Image,
                request.MimeType
            );

            return Ok(new RecognizeTrafficSignResponse
            {
                Result = result
            });
        }
    }
}
