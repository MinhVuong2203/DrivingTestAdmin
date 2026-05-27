using Backend.DTO;
using Backend.Models;
using Backend.Repository;
using Backend.Service;
using Backend.Service.Interface;
using Backend.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DrivingCentersController : ControllerBase
    {
        private readonly IDrivingCenterImportService _drivingCenterImportService;
        private readonly IDrivingCenterService _drivingCenterService;
        public DrivingCentersController(IDrivingCenterImportService drivingCenterImportService, IDrivingCenterService drivingCenterService)
        {
            _drivingCenterImportService = drivingCenterImportService;
            _drivingCenterService = drivingCenterService;
        }

        [HttpPost("import")]
        [AdminAuthorize]
        public async Task<IActionResult> Import([FromBody] ImportDrivingCenterRequest request)
        {
            try
            {
                var result = await _drivingCenterImportService
                    .ImportFromLocalBusinessData(request.query);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Có lỗi khi import dữ liệu trung tâm đào tạo lái xe.",
                    error = ex.Message
                });
            }
        }

  
        // API Flutter gọi để tìm kiếm trung tâm
        // Ví dụ: /api/DrivingCenters/search?keyword=thu duc
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string? keyword,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _drivingCenterService.SearchPaged(keyword, page, pageSize);
            return Ok(result);
        }

        // API Flutter gọi để xem chi tiết 1 trung tâm
        // Ví dụ: /api/DrivingCenters/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<DrivingCenter>> GetById(string id)
        {
            var center = await _drivingCenterService.GetById(id);

            if (center == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy trung tâm."
                });
            }

            return Ok(new
            {
                message = "Lấy chi tiết trung tâm thành công.",
                data = center
            });
        }
    }
}
