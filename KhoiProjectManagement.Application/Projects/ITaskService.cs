using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface ITaskService
    {
        Task<IEnumerable<TaskDto>> GetTasksAsync(TaskFilterDto filter);
        Task<TaskDto?> GetTaskByIdAsync(int id);
        Task<TaskDto> CreateTaskAsync(CreateTaskDto createTaskDto);
        Task<bool> UpdateTaskAsync(int id, UpdateTaskDto updateTaskDto);
        Task<bool> UpdateTaskStatusAsync(int id, string status);
        Task<bool> DeleteTaskAsync(int id);
        Task<IEnumerable<TaskDto>> GetOverdueTasksAsync();
    }
}
