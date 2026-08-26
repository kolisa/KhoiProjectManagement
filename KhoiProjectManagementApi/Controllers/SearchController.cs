using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly IGlobalSearchService _searchService;

        public SearchController(IGlobalSearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet]
        public async Task<ActionResult<GlobalSearchResultDto>> Search([FromQuery] string q)
        {
            return Ok(await _searchService.SearchAsync(q));
        }
    }
}
