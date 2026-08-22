using KhoiProjectManagement.Models.DTOs;

namespace KhoiProjectManagementApi.Services
{
    public interface IDashboardWidgetService
    {
        Task<List<DashboardWidgetCatalogEntryDto>> GetCatalogAsync();
        Task SetAllowlistAsync(List<SetWidgetAllowlistDto> updates);

        // Filtered to only currently-enabled widgets - a widget an admin has since disabled never
        // appears here even if the user previously chose to show it.
        Task<List<DashboardWidgetPreferenceDto>> GetMyPreferencesAsync(int userId);
        Task SetMyPreferencesAsync(int userId, List<SetWidgetPreferenceDto> updates);
    }
}
