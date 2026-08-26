using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface ITaskService
    {
        Task<IEnumerable<TaskDto>> GetTasksAsync(TaskFilterDto filter);
        Task<TaskDto?> GetTaskByIdAsync(int id);
        Task<TaskDto> CreateTaskAsync(CreateTaskDto createTaskDto);
        Task<bool> UpdateTaskAsync(int id, UpdateTaskDto updateTaskDto, int actingUserId);
        Task<bool> UpdateTaskStatusAsync(int id, string status, int actingUserId);
        Task<bool> DeleteTaskAsync(int id);
        Task<IEnumerable<TaskDto>> GetOverdueTasksAsync();
    }
}
