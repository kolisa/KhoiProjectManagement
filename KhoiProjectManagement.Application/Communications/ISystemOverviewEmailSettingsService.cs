namespace KhoiProjectManagement.Application
{
    public interface ISystemOverviewEmailSettingsService
    {
        Task<SystemOverviewEmailSettingsDto> GetAsync();

        Task<SystemOverviewEmailSettingsDto> UpdateAsync(UpdateSystemOverviewEmailSettingsDto dto, int updatedByUserId);
    }
}
