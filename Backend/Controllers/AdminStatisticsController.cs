using Backend.DTO;
using Backend.Filters;
using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/admin/statistics")]
    [ApiController]
    [AdminAuthorize]
    public class AdminStatisticsController : ControllerBase
    {
        private readonly IAdminStatisticsService _statisticsService;

        public AdminStatisticsController(IAdminStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        [HttpGet]
        public async Task<ActionResult<AdminStatisticsResponse>> GetStatistics(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? range = "30d")
        {
            var statistics = await _statisticsService.GetStatistics(from, to, range);
            return Ok(statistics);
        }
    }
}
