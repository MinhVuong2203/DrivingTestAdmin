using Backend.Filters;
using Backend.Models;
using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrafficViolationsController : ControllerBase
    {
        private readonly ITrafficViolationService _service;

        public TrafficViolationsController(ITrafficViolationService service)
        {
            _service = service;
        }

        [HttpPost("import")]
        [AdminAuthorize]
        public async Task<IActionResult> Import()
        {
            try
            {
                var total = await _service.ImportFromJsonAsync();

                return Ok(new
                {
                    message = "Import dữ liệu lỗi vi phạm thành công.",
                    total
                });
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi khi import dữ liệu lỗi vi phạm.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<TrafficViolation>>> Search(
            [FromQuery] string? keyword,
            [FromQuery] string? vehicleType)
        {
            try
            {
                var data = await _service.SearchAsync(keyword, vehicleType);

                return Ok(new
                {
                    message = data.Count == 0
                        ? "Không tìm thấy lỗi vi phạm phù hợp."
                        : "Tìm kiếm lỗi vi phạm thành công.",
                    total = data.Count,
                    data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi khi tìm kiếm lỗi vi phạm.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TrafficViolation>> GetById(string id)
        {
            try
            {
                var violation = await _service.GetByIdAsync(id);

                if (violation == null)
                {
                    return NotFound(new
                    {
                        message = "Không tìm thấy lỗi vi phạm."
                    });
                }

                return Ok(new
                {
                    message = "Lấy chi tiết lỗi vi phạm thành công.",
                    data = violation
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi khi lấy chi tiết lỗi vi phạm.",
                    error = ex.Message
                });
            }
        }
    }
}
