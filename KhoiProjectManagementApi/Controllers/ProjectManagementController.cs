using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagementApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Project Management module API. Authorization is two-layered and lives in IProjectAccessService
    // (module access flag + per-project role) rather than [Authorize(Policy=...)], since the answer
    // depends on which project is being touched. Reachable by module-only accounts.
    [ApiController]
    [Route("api/pm")]
    [Authorize]
    [AllowModuleOnly]
    public class ProjectManagementController : ControllerBase
    {
        private readonly IPmProjectService _projects;
        private readonly IPmWorkItemService _workItems;
        private readonly IPmBoardService _board;

        public ProjectManagementController(IPmProjectService projects, IPmWorkItemService workItems, IPmBoardService board)
        {
            _board = board;
            _projects = projects;
            _workItems = workItems;
        }

        private PmCaller Caller => new(
            int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
            User.HasClaim("superadmin", "true"));

        [HttpGet("projects")]
        public async Task<ActionResult<List<PmProjectSummaryDto>>> GetProjects() =>
            Ok(await _projects.GetMyProjectsAsync(Caller));

        [HttpGet("users")]
        public async Task<ActionResult<List<PmUserLookupDto>>> GetAssignableUsers() =>
            Ok(await _projects.GetAssignableUsersAsync(Caller));

        [HttpGet("projects/{projectId:int}/members")]
        public async Task<ActionResult<List<PmMemberDto>>> GetMembers(int projectId) =>
            Ok(await _projects.GetMembersAsync(Caller, projectId));

        [HttpPut("projects/{projectId:int}/members/{userId:int}")]
        public async Task<IActionResult> SetMember(int projectId, int userId, SetProjectMemberDto dto)
        {
            await _projects.SetMemberAsync(Caller, projectId, userId, dto.Role);
            return NoContent();
        }

        [HttpDelete("projects/{projectId:int}/members/{userId:int}")]
        public async Task<IActionResult> RemoveMember(int projectId, int userId)
        {
            await _projects.RemoveMemberAsync(Caller, projectId, userId);
            return NoContent();
        }

        [HttpGet("projects/{projectId:int}/work-items")]
        public async Task<ActionResult<List<WorkItemDto>>> GetBacklog(int projectId, [FromQuery] WorkItemFilterDto filter) =>
            Ok(await _workItems.GetBacklogAsync(Caller, projectId, filter));

        [HttpPost("projects/{projectId:int}/work-items")]
        public async Task<ActionResult<WorkItemDto>> CreateWorkItem(int projectId, SaveWorkItemDto dto)
        {
            var item = await _workItems.CreateAsync(Caller, projectId, dto);
            return CreatedAtAction(nameof(GetWorkItem), new { itemId = item.Id }, item);
        }

        [HttpGet("work-items/{itemId:int}")]
        public async Task<ActionResult<WorkItemDto>> GetWorkItem(int itemId) =>
            Ok(await _workItems.GetAsync(Caller, itemId));

        [HttpPut("work-items/{itemId:int}")]
        public async Task<ActionResult<WorkItemDto>> UpdateWorkItem(int itemId, SaveWorkItemDto dto) =>
            Ok(await _workItems.UpdateAsync(Caller, itemId, dto));

        [HttpPut("work-items/{itemId:int}/rank")]
        public async Task<ActionResult<WorkItemDto>> RankWorkItem(int itemId, RankWorkItemDto dto) =>
            Ok(await _workItems.RankAsync(Caller, itemId, dto));

        [HttpGet("projects/{projectId:int}/board")]
        public async Task<ActionResult<BoardDto>> GetBoard(int projectId) =>
            Ok(await _board.GetBoardAsync(Caller, projectId));

        [HttpPut("projects/{projectId:int}/board/columns")]
        public async Task<ActionResult<BoardDto>> SaveBoardColumns(int projectId, List<SaveBoardColumnDto> columns) =>
            Ok(await _board.SaveColumnsAsync(Caller, projectId, columns));

        [HttpPut("work-items/{itemId:int}/move")]
        public async Task<ActionResult<WorkItemDto>> MoveWorkItem(int itemId, MoveWorkItemDto dto) =>
            Ok(await _board.MoveAsync(Caller, itemId, dto));

        [HttpDelete("work-items/{itemId:int}")]
        public async Task<IActionResult> DeleteWorkItem(int itemId)
        {
            await _workItems.DeleteAsync(Caller, itemId);
            return NoContent();
        }
    }
}
