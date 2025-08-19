using KhoiProjectManagement.Models.DTOs;

namespace KhoiProjectManagementApi.Services
{
    public interface IProjectService
    {
        Task<IEnumerable<ProjectDto>> GetAllProjectsAsync();
        Task<ProjectDto?> GetProjectByIdAsync(int id);
        Task<ProjectDto> CreateProjectAsync(CreateProjectDto createProjectDto);
        Task<bool> UpdateProjectAsync(int id, UpdateProjectDto updateProjectDto);
        Task<bool> DeleteProjectAsync(int id);
        Task<ProjectStatisticsDto?> GetProjectStatisticsAsync(int id);
    }
}
