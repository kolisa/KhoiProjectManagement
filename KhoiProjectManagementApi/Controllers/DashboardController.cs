using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "dashboard.view")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly IDashboardWidgetService _widgetService;

        public DashboardController(IDashboardService dashboardService, IDashboardWidgetService widgetService)
        {
            _dashboardService = dashboardService;
            _widgetService = widgetService;
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<DashboardStatisticsDto>> GetStatistics()
        {
            var statistics = await _dashboardService.GetDashboardStatisticsAsync();
            return Ok(statistics);
        }

        // Every authenticated user can see the full catalog (including disabled entries) - needed so
        // an admin's own preferences UI can show what's currently off, not just what's on.
        [HttpGet("widgets/catalog")]
        public async Task<IActionResult> GetWidgetCatalog()
        {
            return Ok(await _widgetService.GetCatalogAsync());
        }

        [HttpPut("widgets/allowlist")]
        [Authorize(Policy = "dashboard.manage")]
        public async Task<IActionResult> SetWidgetAllowlist(List<SetWidgetAllowlistDto> updates)
        {
            try
            {
                await _widgetService.SetAllowlistAsync(updates);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("widgets/my-preferences")]
        public async Task<IActionResult> GetMyWidgetPreferences()
        {
            return Ok(await _widgetService.GetMyPreferencesAsync(GetUserId()));
        }

        [HttpPut("widgets/my-preferences")]
        public async Task<IActionResult> SetMyWidgetPreferences(List<SetWidgetPreferenceDto> updates)
        {
            try
            {
                await _widgetService.SetMyPreferencesAsync(GetUserId(), updates);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)!;
            return int.Parse(claim.Value);
        }
    }
}
