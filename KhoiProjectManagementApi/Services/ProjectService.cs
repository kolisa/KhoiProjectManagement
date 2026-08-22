using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class ProjectService : IProjectService
    {
        private readonly ProjectManagementContext _context;
        private readonly INotificationService _notificationService;
        private readonly ISpaceService _spaceService;
        private readonly IEmailService _emailService;

        public ProjectService(ProjectManagementContext context, INotificationService notificationService, ISpaceService spaceService, IEmailService emailService)
        {
            _context = context;
            _notificationService = notificationService;
            _spaceService = spaceService;
            _emailService = emailService;
        }

        public async Task<IEnumerable<ProjectDto>> GetAllProjectsAsync()
        {
            var projects = await _context.Projects
                .Include(p => p.Creator)
                .Include(p => p.ProjectUsers)
                    .ThenInclude(pu => pu.User)
                .Include(p => p.ProjectTags)
                    .ThenInclude(pt => pt.Tag)
                .Include(p => p.Tasks)
                .ToListAsync();

            return projects.Select(MapToDto);
        }

        public async Task<ProjectDto?> GetProjectByIdAsync(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Creator)
                .Include(p => p.ProjectUsers)
                    .ThenInclude(pu => pu.User)
                .Include(p => p.ProjectTags)
                    .ThenInclude(pt => pt.Tag)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id);

            return project == null ? null : MapToDto(project);
        }

        public async Task<ProjectDto> CreateProjectAsync(CreateProjectDto createProjectDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var project = new Project
                {
                    Name = createProjectDto.Name,
                    Description = createProjectDto.Description,
                    Priority = createProjectDto.Priority,
                    StartDate = createProjectDto.StartDate,
                    EndDate = createProjectDto.EndDate,
                    CreatedBy = 1, // This should come from the current user context
                    Status = "active"
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                // Add team members
                if (createProjectDto.TeamMemberIds?.Any() == true)
                {
                    var projectUsers = createProjectDto.TeamMemberIds.Select(userId => new ProjectUser
                    {
                        ProjectId = project.Id,
                        UserId = userId
                    });

                    _context.ProjectUsers.AddRange(projectUsers);

                    var spaceId = await _spaceService.EnsureProjectSpaceAsync(project.Id, project.CreatedBy);
                    await _spaceService.SyncSpaceMembersAsync(spaceId, createProjectDto.TeamMemberIds, PermissionLevel.Write, project.CreatedBy);
                }

                // Add tags
                if (createProjectDto.Tags?.Any() == true)
                {
                    await AddProjectTagsAsync(project.Id, createProjectDto.Tags);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Send notifications
                if (createProjectDto.TeamMemberIds?.Any() == true)
                {
                    foreach (var userId in createProjectDto.TeamMemberIds)
                    {
                        await _notificationService.CreateNotificationAsync(
                            userId,
                            "project_created",
                            $"You have been added to project '{project.Name}'",
                            projectId: project.Id
                        );

                        // Previously unused - SendProjectCreatedEmailAsync existed on IEmailService but
                        // was never actually called anywhere; wiring it up here now that email sends are
                        // gated by preference, not before.
                        if (await _notificationService.IsEmailEnabledAsync(userId, NotificationTypes.ProjectCreated))
                        {
                            var member = await _context.Users.FindAsync(userId);
                            if (member != null)
                            {
                                try
                                {
                                    await _emailService.SendProjectCreatedEmailAsync(member.Email, project.Name);
                                }
                                catch
                                {
                                    // Project already committed - a failed SMTP send must never fail
                                    // project creation. Already logged to EmailLog by EmailService.
                                }
                            }
                        }
                    }
                }

                // Reload to get full data
                return await GetProjectByIdAsync(project.Id) ?? throw new InvalidOperationException("Project not found after creation");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> UpdateProjectAsync(int id, UpdateProjectDto updateProjectDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var project = await _context.Projects
                    .Include(p => p.ProjectUsers)
                    .Include(p => p.ProjectTags)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (project == null)
                    return false;

                project.Name = updateProjectDto.Name;
                project.Description = updateProjectDto.Description;
                project.Priority = updateProjectDto.Priority;
                project.Status = updateProjectDto.Status;
                project.StartDate = updateProjectDto.StartDate;
                project.EndDate = updateProjectDto.EndDate;

                // Update team members
                _context.ProjectUsers.RemoveRange(project.ProjectUsers);
                if (updateProjectDto.TeamMemberIds?.Any() == true)
                {
                    var projectUsers = updateProjectDto.TeamMemberIds.Select(userId => new ProjectUser
                    {
                        ProjectId = project.Id,
                        UserId = userId
                    });
                    _context.ProjectUsers.AddRange(projectUsers);

                    var spaceId = await _spaceService.EnsureProjectSpaceAsync(project.Id, project.CreatedBy);
                    await _spaceService.SyncSpaceMembersAsync(spaceId, updateProjectDto.TeamMemberIds, PermissionLevel.Write, project.CreatedBy);
                }
                else if (project.SpaceId.HasValue)
                {
                    // All team members were removed - clear the project Space's member grants too.
                    await _spaceService.SyncSpaceMembersAsync(project.SpaceId.Value, Array.Empty<int>(), PermissionLevel.Write, project.CreatedBy);
                }

                // Update tags
                _context.ProjectTags.RemoveRange(project.ProjectTags);
                if (updateProjectDto.Tags?.Any() == true)
                {
                    await AddProjectTagsAsync(project.Id, updateProjectDto.Tags);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> DeleteProjectAsync(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return false;

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ProjectStatisticsDto?> GetProjectStatisticsAsync(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return null;

            var totalTasks = project.Tasks.Count;
            var completedTasks = project.Tasks.Count(t => t.Status == "completed");
            var inProgressTasks = project.Tasks.Count(t => t.Status == "in-progress");
            var todoTasks = project.Tasks.Count(t => t.Status == "todo");
            var overdueTasks = project.Tasks.Count(t => t.IsOverdue);

            return new ProjectStatisticsDto
            {
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                InProgressTasks = inProgressTasks,
                TodoTasks = todoTasks,
                OverdueTasks = overdueTasks,
                CompletionRate = totalTasks == 0 ? 0 : (double)completedTasks / totalTasks * 100
            };
        }

        private async Task AddProjectTagsAsync(int projectId, IEnumerable<string> tagNames)
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

                _context.ProjectTags.Add(new ProjectTag
                {
                    ProjectId = projectId,
                    TagId = tag.Id
                });
            }
        }

        private static ProjectDto MapToDto(Project project)
        {
            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                Status = project.Status,
                Priority = project.Priority,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                CreatedAt = project.CreatedAt,
                CreatorName = project.Creator?.Name ?? "Unknown",
                Tags = project.ProjectTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>(),
                TeamMembers = project.ProjectUsers?.Select(pu => new TeamMemberDto
                {
                    Id = pu.User.Id,
                    Name = pu.User.Name,
                    Email = pu.User.Email,
                    Role = pu.User.Role,
                    Position = pu.User.Position,
                    IsActive = pu.User.IsActive
                }).ToList() ?? new List<TeamMemberDto>(),
                TaskCount = project.Tasks?.Count ?? 0,
                CompletedTaskCount = project.Tasks?.Count(t => t.Status == "completed") ?? 0
            };
        }
    }
}
