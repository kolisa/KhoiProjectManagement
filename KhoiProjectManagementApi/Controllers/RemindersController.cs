using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Personal, ownership-first (own reminders need no permission) - reminders.view_all/reminders.manage
    // gate cross-user visibility/assignment, mirroring the Timesheets/HR/Ideas pattern established
    // elsewhere in this app. See ReminderService for the actual access checks.
    [ApiController]
    [Route("api/reminders")]
    [Authorize]
    public class RemindersController : ControllerBase
    {
        private readonly IReminderService _reminderService;

        public RemindersController(IReminderService reminderService)
        {
            _reminderService = reminderService;
        }

        [HttpGet]
        public async Task<IActionResult> GetReminders([FromQuery] ReminderFilterDto filter)
        {
            return Ok(await _reminderService.GetRemindersAsync(filter, User));
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            return Ok(await _reminderService.GetSummaryCountsAsync(User));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetReminder(int id)
        {
            try
            {
                var reminder = await _reminderService.GetReminderByIdAsync(id, User);
                if (reminder == null)
                    return NotFound();

                return Ok(reminder);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateReminder(CreateReminderDto dto)
        {
            try
            {
                var reminder = await _reminderService.CreateReminderAsync(dto, User);
                return CreatedAtAction(nameof(GetReminder), new { id = reminder.Id }, reminder);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateReminder(int id, UpdateReminderDto dto)
        {
            try
            {
                var updated = await _reminderService.UpdateReminderAsync(id, dto, User);
                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteReminder(int id)
        {
            try
            {
                var deleted = await _reminderService.DeleteReminderAsync(id, User);
                if (!deleted)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("{id:int}/complete")]
        public async Task<IActionResult> Complete(int id)
        {
            try
            {
                var completed = await _reminderService.CompleteAsync(id, User);
                if (!completed)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("{id:int}/reopen")]
        public async Task<IActionResult> Reopen(int id)
        {
            try
            {
                var reopened = await _reminderService.ReopenAsync(id, User);
                if (!reopened)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("{id:int}/snooze")]
        public async Task<IActionResult> Snooze(int id, SnoozeReminderDto dto)
        {
            try
            {
                var snoozed = await _reminderService.SnoozeAsync(id, dto, User);
                if (!snoozed)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("{id:int}/duplicate")]
        public async Task<IActionResult> Duplicate(int id)
        {
            try
            {
                var copy = await _reminderService.DuplicateAsync(id, User);
                return CreatedAtAction(nameof(GetReminder), new { id = copy.Id }, copy);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("bulk/complete")]
        public async Task<IActionResult> BulkComplete(BulkReminderActionDto dto)
        {
            var count = await _reminderService.BulkCompleteAsync(dto, User);
            return Ok(new { count });
        }

        [HttpDelete("bulk")]
        public async Task<IActionResult> BulkDelete(BulkReminderActionDto dto)
        {
            var count = await _reminderService.BulkDeleteAsync(dto, User);
            return Ok(new { count });
        }

        [HttpPut("bulk/reschedule")]
        public async Task<IActionResult> BulkReschedule(BulkRescheduleReminderDto dto)
        {
            var count = await _reminderService.BulkRescheduleAsync(dto, User);
            return Ok(new { count });
        }

        [HttpPut("bulk/priority")]
        public async Task<IActionResult> BulkPriority(BulkPriorityReminderDto dto)
        {
            try
            {
                var count = await _reminderService.BulkPriorityAsync(dto, User);
                return Ok(new { count });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("bulk/assign")]
        [Authorize(Policy = "reminders.manage")]
        public async Task<IActionResult> BulkAssign(BulkAssignReminderDto dto)
        {
            var count = await _reminderService.BulkAssignAsync(dto, User);
            return Ok(new { count });
        }
    }
}
