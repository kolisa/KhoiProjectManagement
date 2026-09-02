using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class TaskService : ITaskService
    {
        private readonly IRepository<ProjectTask> _taskRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<Project> _projectRepo;
        private readonly IRepository<TaskTag> _taskTagRepo;
        private readonly IRepository<Tag> _tagRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly IActivityLogService _activityLogService;

        public TaskService(
            IRepository<ProjectTask> taskRepo,
            IRepository<User> userRepo,
            IRepository<Project> projectRepo,
            IRepository<TaskTag> taskTagRepo,
            IRepository<Tag> tagRepo,
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            IEmailService emailService,
            IActivityLogService activityLogService)
        {
            _taskRepo = taskRepo;
            _userRepo = userRepo;
            _projectRepo = projectRepo;
            _taskTagRepo = taskTagRepo;
            _tagRepo = tagRepo;
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _emailService = emailService;
            _activityLogService = activityLogService;
        }

        public async Task<IEnumerable<TaskDto>> GetTasksAsync(TaskFilterDto filter)
        {
            var query = _taskRepo.Query()
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
            var task = await _taskRepo.Query()
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.TaskTags)
                    .ThenInclude(tt => tt.Tag)
                .FirstOrDefaultAsync(t => t.Id == id);

            return task == null ? null : MapToDto(task);
        }

        public async Task<TaskDto> CreateTaskAsync(CreateTaskDto createTaskDto)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var task = new ProjectTask
                {
                    ProjectId = createTaskDto.ProjectId,
                    Title = createTaskDto.Title,
                    Description = createTaskDto.Description,
                    Priority = createTaskDto.Priority,
                    Type = createTaskDto.Type,
                    AssignedToId = createTaskDto.AssignedToId,
                    DueDate = createTaskDto.DueDate,
                    Status = "todo"
                };

                _taskRepo.Add(task);
                await _unitOfWork.SaveChangesAsync();

                // Add tags
                if (createTaskDto.Tags?.Any() == true)
                {
                    await AddTaskTagsAsync(task.Id, createTaskDto.Tags);
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                // Send notifications
                if (task.AssignedToId.HasValue)
                {
                    var assignedUser = await _userRepo.FindAsync(task.AssignedToId.Value);
                    var project = await _projectRepo.FindAsync(task.ProjectId);

                    if (assignedUser != null && project != null)
                    {
                        await _notificationService.CreateNotificationAsync(
                            task.AssignedToId.Value,
                            "assignment",
                            $"You have been assigned to task '{task.Title}' in project '{project.Name}'",
                            taskId: task.Id
                        );

                        if (await _notificationService.IsEmailEnabledAsync(task.AssignedToId.Value, NotificationTypes.Assignment))
                        {
                            // A failed SMTP send must never fail task creation - the task itself already
                            // committed. EmailService already records the failure to EmailLog.
                            try
                            {
                                await _emailService.SendTaskAssignmentEmailAsync(
                                    assignedUser.Email,
                                    task.Title,
                                    project.Name
                                );
                            }
                            catch
                            {
                                // Already logged to EmailLog by EmailService - intentionally swallowed.
                            }
                        }
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

        public async Task<bool> UpdateTaskAsync(int id, UpdateTaskDto updateTaskDto, int actingUserId)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var task = await _taskRepo.Query()
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
                task.Type = updateTaskDto.Type;
                task.AssignedToId = updateTaskDto.AssignedToId;
                task.DueDate = updateTaskDto.DueDate;

                if (updateTaskDto.Status == "completed" && oldStatus != "completed")
                {
                    task.CompletedAt = DateTime.UtcNow;
                }

                // Update tags
                _taskTagRepo.RemoveRange(task.TaskTags);
                if (updateTaskDto.Tags?.Any() == true)
                {
                    await AddTaskTagsAsync(task.Id, updateTaskDto.Tags);
                }

                await _unitOfWork.SaveChangesAsync();
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

                    await _activityLogService.LogAsync("Task", task.Id, task.Title, actingUserId, "Completed");
                }

                // Send notification for assignment changes
                if (oldAssignedTo != updateTaskDto.AssignedToId && updateTaskDto.AssignedToId.HasValue)
                {
                    var assignedUser = await _userRepo.FindAsync(updateTaskDto.AssignedToId.Value);
                    var project = await _projectRepo.FindAsync(task.ProjectId);

                    if (assignedUser != null && project != null)
                    {
                        await _notificationService.CreateNotificationAsync(
                            updateTaskDto.AssignedToId.Value,
                            "assignment",
                            $"You have been assigned to task '{task.Title}' in project '{project.Name}'",
                            taskId: task.Id
                        );

                        if (await _notificationService.IsEmailEnabledAsync(updateTaskDto.AssignedToId.Value, NotificationTypes.Assignment))
                        {
                            try
                            {
                                await _emailService.SendTaskAssignmentEmailAsync(
                                    assignedUser.Email,
                                    task.Title,
                                    project.Name
                                );
                            }
                            catch
                            {
                                // Already logged to EmailLog by EmailService - intentionally swallowed.
                            }
                        }
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

        public async Task<bool> UpdateTaskStatusAsync(int id, string status, int actingUserId)
        {
            var task = await _taskRepo.FindAsync(id);
            if (task == null)
                return false;

            var oldStatus = task.Status;
            task.Status = status;

            if (status == "completed" && oldStatus != "completed")
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync();

            // Send notification
            if (oldStatus != status && status == "completed")
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

                await _activityLogService.LogAsync("Task", task.Id, task.Title, actingUserId, "Completed");
            }

            return true;
        }

        public async Task<bool> DeleteTaskAsync(int id)
        {
            var task = await _taskRepo.FindAsync(id);
            if (task == null)
                return false;

            _taskRepo.Remove(task);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<TaskDto>> GetOverdueTasksAsync()
        {
            var overdueTasks = await _taskRepo.Query()
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
                var tag = await _tagRepo.Query().FirstOrDefaultAsync(t => t.Name == tagName.ToLower());
                if (tag == null)
                {
                    tag = new Tag { Name = tagName.ToLower() };
                    _tagRepo.Add(tag);
                    await _unitOfWork.SaveChangesAsync();
                }

                _taskTagRepo.Add(new TaskTag
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
                Type = task.Type,
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
