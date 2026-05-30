using Backend.Service.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdMobReportController : ControllerBase
    {
        private readonly IAdMobService _admobService;

        public AdMobReportController(IAdMobService admobService)
        {
            _admobService = admobService;
        }

        /// <summary>Báo cáo doanh thu theo ngày</summary>
        [HttpGet("report")]
        public async Task<IActionResult> GetReport(
            [FromQuery] string startDate,
            [FromQuery] string endDate)
        {
            try
            {
                var data = await _admobService.GetNetworkReportAsync(
                    startDate, endDate);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Tổng doanh thu tháng hiện tại</summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var now = DateTime.Now;
            var start = $"{now.Year}-{now.Month:D2}-01";
            var end = $"{now.Year}-{now.Month:D2}-{now.Day:D2}";

            try
            {
                var data = await _admobService.GetNetworkReportAsync(start, end);
                return Ok(new
                {
                    TotalEarnings = data.Sum(x => x.EstimatedEarnings),
                    TotalImpressions = data.Sum(x => x.Impressions),
                    TotalClicks = data.Sum(x => x.Clicks),
                    AvgEcpm = data.Any() ? data.Average(x => x.Ecpm) : 0,
                    Period = new { startDate = start, endDate = end }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}