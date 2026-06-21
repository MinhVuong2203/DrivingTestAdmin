using Backend.Filters;
using Backend.Models;
using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SimulationSituationsController : ControllerBase
    {
        private readonly ISimulationSituationService _service;

        public SimulationSituationsController(ISimulationSituationService service)
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
                    message = "Import du lieu tinh huong mo phong thanh cong.",
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
                    message = "Co loi khi import du lieu tinh huong mo phong.",
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<SimulationSituation>>> GetAll()
        {
            try
            {
                var data = await _service.GetAllAsync();

                return Ok(new
                {
                    message = "Lay danh sach tinh huong mo phong thanh cong.",
                    total = data.Count,
                    data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Co loi khi lay danh sach tinh huong mo phong.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("{docId}")]
        public async Task<ActionResult<SimulationSituation>> GetById(string docId)
        {
            try
            {
                var situation = await _service.GetByIdAsync(docId);

                if (situation == null)
                {
                    return NotFound(new
                    {
                        message = "Khong tim thay tinh huong mo phong."
                    });
                }

                return Ok(new
                {
                    message = "Lay chi tiet tinh huong mo phong thanh cong.",
                    data = situation
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Co loi khi lay chi tiet tinh huong mo phong.",
                    error = ex.Message
                });
            }
        }
    }
}
