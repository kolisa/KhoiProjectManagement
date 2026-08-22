using System.Security.Claims;
using KhoiProjectManagement.Models.DTOs;

namespace KhoiProjectManagementApi.Services
{
    public interface IHrService
    {
        Task<List<OnboardingTemplateDto>> GetTemplatesAsync();
        Task<OnboardingTemplateDto> CreateTemplateAsync(CreateOnboardingTemplateDto dto);
        Task<bool> UpdateTemplateAsync(int id, UpdateOnboardingTemplateDto dto);

        // Ownership-or-permission checks (hr.view / hr.manage vs "it's my own checklist") happen inside
        // the service, same reasoning as VaultService re-checking Space access per entity - a blanket
        // [Authorize] attribute can't see whose checklist this is.
        Task<List<OnboardingChecklistDto>> GetChecklistsAsync(int? userId, ClaimsPrincipal caller);
        Task<OnboardingChecklistDto?> GetChecklistByIdAsync(int id, ClaimsPrincipal caller);
        Task<OnboardingChecklistDto> CreateChecklistAsync(CreateOnboardingChecklistDto dto);
        Task<bool> UpdateChecklistItemAsync(int checklistId, int itemId, UpdateChecklistItemDto dto, ClaimsPrincipal caller);
    }
}
