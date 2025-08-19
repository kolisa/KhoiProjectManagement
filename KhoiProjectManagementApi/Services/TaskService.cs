using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class TaskService : ITaskService
    {
        private readonly ProjectManagementContext _context;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;

        public TaskService(ProjectManagementContext context, INotificationService notificationService, IEmailService emailService)
        {
            _context = context;
            _notificationService = notificationService;
            _emailService = emailService;
        }

        public async Task<IEnumerable<TaskDto>> GetTasksAsync(TaskFilterDto filter)
        {
            var query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.TaskTags)
                    .ThenInclude(tt => tt.Tag)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(filter.Status))
            {
                query = query.Where(t => t.Status == filter.Status);
            }

            if (!string.IsNullOrEmpty(filter.Priority))
            {
                query = query.Where(t => t.Priority == filter.Priority);
            }

            if (filter.ProjectId.HasValue)
            {
                query = query.Where(t => t.ProjectId == filter.ProjectId.Value);
            }

            if (filter.AssignedToId.HasValue)
            {
                query = query.Where(t => t.AssignedToId == filter.AssignedToId.Value);
            }

            if (filter.IsOverdue.HasValue && filter.IsOverdue.Value)
            {
                query = query.Where(t => t.Status != "completed" && t.DueDate < DateTime.UtcNow);
            }

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(t => t.Title.Contains(filter.SearchTerm) ||
                                       t.Description.Contains(filter.SearchTerm));
            }

            var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
            return tasks.Select(MapToDto);
        }

        public async Task<TaskDto?> GetTaskByIdAsync(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.TaskTags)
                    .ThenInclude(tt => tt.Tag)
                .FirstOrDefaultAsync(t => t.Id == id);

            return task == null ? null : MapToDto(task);
        }

        public async Task<TaskDto> CreateTaskAsync(CreateTaskDto createTaskDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var task = new ProjectTask
                {
                    ProjectId = createTaskDto.ProjectId,
                    Title = createTaskDto.Title,
                    Description = createTaskDto.Description,
                    Priority = createTaskDto.Priority,
                    AssignedToId = createTaskDto.AssignedToId,
                    DueDate = createTaskDto.DueDate,
                    Status = "todo"
                };

                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();

                // Add tags
                if (createTaskDto.Tags?.Any() == true)
                {
                    await AddTaskTagsAsync(task.Id, createTaskDto.Tags);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Send notifications
                if (task.AssignedToId.HasValue)
                {
                    var assignedUser = await _context.Users.FindAsync(task.AssignedToId.Value);
                    var project = await _context.Projects.FindAsync(task.ProjectId);

                    if (assignedUser != null && project != null)
                    {
                        await _notificationService.CreateNotificationAsync(
                            task.AssignedToId.Value,
                            "assignment",
                            $"You have been assigned to task '{task.Title}' in project '{project.Name}'",
                            taskId: task.Id
                        );

                        await _emailService.SendTaskAssignmentEmailAsync(
                            assignedUser.Email,
                            task.Title,
                            project.Name
                        );
                    }
                }

                // Reload to get full data
                return await GetTaskByIdAsync(task.Id) ?? throw new InvalidOperationException("Task not found after creation");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> UpdateTaskAsync(int id, UpdateTaskDto updateTaskDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var task = await _context.Tasks
                    .Include(t => t.TaskTags)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (task == null)
                    return false;

                var oldStatus = task.Status;
                var oldAssignedTo = task.AssignedToId;

                task.Title = updateTaskDto.Title;
                task.Description = updateTaskDto.Description;
                task.Status = updateTaskDto.Status;
                task.Priority = updateTaskDto.Priority;
                task.AssignedToId = updateTaskDto.AssignedToId;
                task.DueDate = updateTaskDto.DueDate;

                if (updateTaskDto.Status == "completed" && oldStatus != "completed")
                {
                    task.CompletedAt = DateTime.UtcNow;
                }

                // Update tags
                _context.TaskTags.RemoveRange(task.TaskTags);
                if (updateTaskDto.Tags?.Any() == true)
                {
                    await AddTaskTagsAsync(task.Id, updateTaskDto.Tags);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Send notifications for status changes
                if (oldStatus != updateTaskDto.Status && updateTaskDto.Status == "completed")
                {
                    if (task.AssignedToId.HasValue)
                    {
                        await _notificationService.CreateNotificationAsync(
                            task.AssignedToId.Value,
                            "completion",
                            $"Task '{task.Title}' has been marked as completed",
                            taskId: task.Id
                        );
                    }
                }

                // Send notification for assignment changes
                if (oldAssignedTo != updateTaskDto.AssignedToId && updateTaskDto.AssignedToId.HasValue)
                {
                    var assignedUser = await _context.Users.FindAsync(updateTaskDto.AssignedToId.Value);
                    var project = await _context.Projects.FindAsync(task.ProjectId);

                    if (assignedUser != null && project != null)
                    {
                        await _notificationService.CreateNotificationAsync(
                            updateTaskDto.AssignedToId.Value,
                            "assignment",
                            $"You have been assigned to task '{task.Title}' in project '{project.Name}'",
                            taskId: task.Id
                        );

                        await _emailService.SendTaskAssignmentEmailAsync(
                            assignedUser.Email,
                            task.Title,
                            project.Name
                        );
                    }
                }

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> UpdateTaskStatusAsync(int id, string status)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null)
                return false;

            var oldStatus = task.Status;
            task.Status = status;

            if (status == "completed" && oldStatus != "completed")
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Send notification
            if (oldStatus != status && status == "completed" && task.AssignedToId.HasValue)
            {
                await _notificationService.CreateNotificationAsync(
                    task.AssignedToId.Value,
                    "completion",
                    $"Task '{task.Title}' has been marked as completed",
                    taskId: task.Id
                );
            }

            return true;
        }

        public async Task<bool> DeleteTaskAsync(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null)
                return false;

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<TaskDto>> GetOverdueTasksAsync()
        {
            var overdueTasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.TaskTags)
                    .ThenInclude(tt => tt.Tag)
                .Where(t => t.Status != "completed" && t.DueDate < DateTime.UtcNow)
                .OrderBy(t => t.DueDate)
                .ToListAsync();

            return overdueTasks.Select(MapToDto);
        }

        private async Task AddTaskTagsAsync(int taskId, IEnumerable<string> tagNames)
        {
            foreach (var tagName in tagNames)
            {
                var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName.ToLower());
                if (tag == null)
                {
                    tag = new Tag { Name = tagName.ToLower() };
                    _context.Tags.Add(tag);
                    await _context.SaveChangesAsync();
                }

                _context.TaskTags.Add(new TaskTag
                {
                    TaskId = taskId,
                    TagId = tag.Id
                });
            }
        }

        private static TaskDto MapToDto(ProjectTask task)
        {
            return new TaskDto
            {
                Id = task.Id,
                ProjectId = task.ProjectId,
                ProjectName = task.Project?.Name ?? "Unknown Project",
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Priority = task.Priority,
                AssignedToId = task.AssignedToId,
                AssignedToName = task.AssignedTo?.Name ?? "Unassigned",
                DueDate = task.DueDate,
                CreatedAt = task.CreatedAt,
                CompletedAt = task.CompletedAt,
                IsOverdue = task.IsOverdue,
                Tags = task.TaskTags?.Select(tt => tt.Tag.Name).ToList() ?? new List<string>()
            };
        }
    }
}
