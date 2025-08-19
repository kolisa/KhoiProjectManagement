using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
   // [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<DashboardStatisticsDto>> GetStatistics()
        {
            var statistics = await _dashboardService.GetDashboardStatisticsAsync();
            return Ok(statistics);
        }
    }
}
