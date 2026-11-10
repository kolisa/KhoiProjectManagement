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
        private readonly IPmSprintService _sprints;
        private readonly IPmCollaborationService _collab;

        public ProjectManagementController(IPmProjectService projects, IPmWorkItemService workItems, IPmBoardService board, IPmSprintService sprints, IPmCollaborationService collab)
        {
            _sprints = sprints;
            _collab = collab;
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
        public async Task<ActionResult<BoardDto>> GetBoard(int projectId, [FromQuery] int? sprintId) =>
            Ok(await _board.GetBoardAsync(Caller, projectId, sprintId));

        [HttpPut("projects/{projectId:int}/board/columns")]
        public async Task<ActionResult<BoardDto>> SaveBoardColumns(int projectId, List<SaveBoardColumnDto> columns) =>
            Ok(await _board.SaveColumnsAsync(Caller, projectId, columns));

        [HttpPut("work-items/{itemId:int}/move")]
        public async Task<ActionResult<WorkItemDto>> MoveWorkItem(int itemId, MoveWorkItemDto dto) =>
            Ok(await _board.MoveAsync(Caller, itemId, dto));

        [HttpGet("projects/{projectId:int}/sprints")]
        public async Task<ActionResult<List<SprintDto>>> GetSprints(int projectId) =>
            Ok(await _sprints.GetSprintsAsync(Caller, projectId));

        [HttpPost("projects/{projectId:int}/sprints")]
        public async Task<ActionResult<SprintDto>> CreateSprint(int projectId, SaveSprintDto dto) =>
            Ok(await _sprints.CreateAsync(Caller, projectId, dto));

        [HttpPut("sprints/{sprintId:int}")]
        public async Task<ActionResult<SprintDto>> UpdateSprint(int sprintId, SaveSprintDto dto) =>
            Ok(await _sprints.UpdateAsync(Caller, sprintId, dto));

        [HttpDelete("sprints/{sprintId:int}")]
        public async Task<IActionResult> DeleteSprint(int sprintId)
        {
            await _sprints.DeleteAsync(Caller, sprintId);
            return NoContent();
        }

        [HttpPost("sprints/{sprintId:int}/start")]
        public async Task<ActionResult<SprintDto>> StartSprint(int sprintId) =>
            Ok(await _sprints.StartAsync(Caller, sprintId));

        [HttpPost("sprints/{sprintId:int}/complete")]
        public async Task<ActionResult<SprintDto>> CompleteSprint(int sprintId, CompleteSprintDto dto) =>
            Ok(await _sprints.CompleteAsync(Caller, sprintId, dto));

        [HttpGet("sprints/{sprintId:int}/burndown")]
        public async Task<ActionResult<BurndownDto>> GetBurndown(int sprintId) =>
            Ok(await _sprints.GetBurndownAsync(Caller, sprintId));

        [HttpGet("projects/{projectId:int}/velocity")]
        public async Task<ActionResult<VelocityDto>> GetVelocity(int projectId, [FromQuery] int count = 6) =>
            Ok(await _sprints.GetVelocityAsync(Caller, projectId, count));

        [HttpPut("work-items/{itemId:int}/sprint")]
        public async Task<ActionResult<WorkItemDto>> AssignSprint(int itemId, AssignSprintDto dto) =>
            Ok(await _sprints.AssignAsync(Caller, itemId, dto.SprintId));

        // ---- Phase 4: collaboration (comments, checklist, links, attachments) ----

        [HttpGet("work-items/{itemId:int}/comments")]
        public async Task<ActionResult<List<WorkItemCommentDto>>> GetComments(int itemId) =>
            Ok(await _collab.GetCommentsAsync(Caller, itemId));

        [HttpPost("work-items/{itemId:int}/comments")]
        public async Task<ActionResult<WorkItemCommentDto>> AddComment(int itemId, SaveCommentDto dto) =>
            Ok(await _collab.AddCommentAsync(Caller, itemId, dto));

        [HttpPut("comments/{commentId:int}")]
        public async Task<ActionResult<WorkItemCommentDto>> EditComment(int commentId, SaveCommentDto dto) =>
            Ok(await _collab.EditCommentAsync(Caller, commentId, dto));

        [HttpDelete("comments/{commentId:int}")]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            await _collab.DeleteCommentAsync(Caller, commentId);
            return NoContent();
        }

        [HttpGet("work-items/{itemId:int}/checklist")]
        public async Task<ActionResult<List<ChecklistItemDto>>> GetChecklist(int itemId) =>
            Ok(await _collab.GetChecklistAsync(Caller, itemId));

        [HttpPost("work-items/{itemId:int}/checklist")]
        public async Task<ActionResult<ChecklistItemDto>> AddChecklistItem(int itemId, SaveChecklistItemDto dto) =>
            Ok(await _collab.AddChecklistItemAsync(Caller, itemId, dto));

        [HttpPut("checklist/{checklistItemId:int}")]
        public async Task<ActionResult<ChecklistItemDto>> UpdateChecklistItem(int checklistItemId, SaveChecklistItemDto dto) =>
            Ok(await _collab.UpdateChecklistItemAsync(Caller, checklistItemId, dto));

        [HttpDelete("checklist/{checklistItemId:int}")]
        public async Task<IActionResult> DeleteChecklistItem(int checklistItemId)
        {
            await _collab.DeleteChecklistItemAsync(Caller, checklistItemId);
            return NoContent();
        }

        [HttpGet("work-items/{itemId:int}/links")]
        public async Task<ActionResult<List<WorkItemLinkDto>>> GetLinks(int itemId) =>
            Ok(await _collab.GetLinksAsync(Caller, itemId));

        [HttpPost("work-items/{itemId:int}/links")]
        public async Task<ActionResult<WorkItemLinkDto>> AddLink(int itemId, CreateWorkItemLinkDto dto) =>
            Ok(await _collab.AddLinkAsync(Caller, itemId, dto));

        [HttpDelete("links/{linkId:int}")]
        public async Task<IActionResult> DeleteLink(int linkId)
        {
            await _collab.DeleteLinkAsync(Caller, linkId);
            return NoContent();
        }

        [HttpGet("work-items/{itemId:int}/attachments")]
        public async Task<ActionResult<List<WorkItemAttachmentDto>>> GetAttachments(int itemId) =>
            Ok(await _collab.GetAttachmentsAsync(Caller, itemId));

        [HttpPost("work-items/{itemId:int}/attachments")]
        public async Task<ActionResult<WorkItemAttachmentDto>> UploadAttachment(int itemId, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { message = "No file provided." });
            await using var stream = file.OpenReadStream();
            return Ok(await _collab.AddAttachmentAsync(Caller, itemId, new AttachmentUploadDto
            {
                Content = stream, FileName = file.FileName, ContentType = file.ContentType, Length = file.Length,
            }));
        }

        [HttpGet("attachments/{attachmentId:int}/download")]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var download = await _collab.DownloadAttachmentAsync(Caller, attachmentId);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            // Always sent as a download (Content-Disposition: attachment), never rendered inline from this origin.
            return File(download.Content, string.IsNullOrWhiteSpace(download.ContentType) ? "application/octet-stream" : download.ContentType, download.FileName);
        }

        [HttpDelete("attachments/{attachmentId:int}")]
        public async Task<IActionResult> DeleteAttachment(int attachmentId)
        {
            await _collab.DeleteAttachmentAsync(Caller, attachmentId);
            return NoContent();
        }

        [HttpDelete("work-items/{itemId:int}")]
        public async Task<IActionResult> DeleteWorkItem(int itemId)
        {
            await _workItems.DeleteAsync(Caller, itemId);
            return NoContent();
        }
    }
}
