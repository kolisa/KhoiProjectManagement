using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class DashboardWidgetService : IDashboardWidgetService
    {
        private readonly IRepository<DashboardWidgetAllowlist> _allowlistRepo;
        private readonly IRepository<DashboardWidgetPreference> _preferenceRepo;
        private readonly IUnitOfWork _unitOfWork;

        public DashboardWidgetService(IRepository<DashboardWidgetAllowlist> allowlistRepo, IRepository<DashboardWidgetPreference> preferenceRepo, IUnitOfWork unitOfWork)
        {
            _allowlistRepo = allowlistRepo;
            _preferenceRepo = preferenceRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<DashboardWidgetCatalogEntryDto>> GetCatalogAsync()
        {
            var allowlist = await _allowlistRepo.Query()
                .ToDictionaryAsync(a => a.WidgetKey, a => a.IsEnabled);

            return DashboardWidgetTypes.Catalog
                .OrderBy(c => c.CatalogOrder)
                .Select(c => new DashboardWidgetCatalogEntryDto
                {
                    WidgetKey = c.Key,
                    DisplayName = c.DisplayName,
                    Description = c.Description,
                    IsEnabled = allowlist.TryGetValue(c.Key, out var enabled) ? enabled : true
                }).ToList();
        }

        public async Task SetAllowlistAsync(List<SetWidgetAllowlistDto> updates)
        {
            foreach (var update in updates)
            {
                if (!DashboardWidgetTypes.IsValid(update.WidgetKey))
                    throw new InvalidOperationException($"Unknown widget key '{update.WidgetKey}'.");

                var existing = await _allowlistRepo.Query()
                    .FirstOrDefaultAsync(a => a.WidgetKey == update.WidgetKey);

                if (existing == null)
                {
                    _allowlistRepo.Add(new DashboardWidgetAllowlist
                    {
                        WidgetKey = update.WidgetKey,
                        IsEnabled = update.IsEnabled
                    });
                }
                else
                {
                    existing.IsEnabled = update.IsEnabled;
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<List<DashboardWidgetPreferenceDto>> GetMyPreferencesAsync(int userId)
        {
            var allowlist = await _allowlistRepo.Query()
                .ToDictionaryAsync(a => a.WidgetKey, a => a.IsEnabled);

            var userPrefs = await _preferenceRepo.Query()
                .Where(p => p.UserId == userId)
                .ToDictionaryAsync(p => p.WidgetKey);

            var enabledCatalog = DashboardWidgetTypes.Catalog
                .Where(c => allowlist.TryGetValue(c.Key, out var enabled) ? enabled : true);

            return enabledCatalog
                .Select(c =>
                {
                    userPrefs.TryGetValue(c.Key, out var pref);
                    return new DashboardWidgetPreferenceDto
                    {
                        WidgetKey = c.Key,
                        DisplayName = c.DisplayName,
                        IsVisible = pref?.IsVisible ?? true,
                        SortOrder = pref?.SortOrder ?? c.CatalogOrder
                    };
                })
                .OrderBy(w => w.SortOrder)
                .ToList();
        }

        public async Task SetMyPreferencesAsync(int userId, List<SetWidgetPreferenceDto> updates)
        {
            foreach (var update in updates)
            {
                if (!DashboardWidgetTypes.IsValid(update.WidgetKey))
                    throw new InvalidOperationException($"Unknown widget key '{update.WidgetKey}'.");

                var existing = await _preferenceRepo.Query()
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.WidgetKey == update.WidgetKey);

                if (existing == null)
                {
                    _preferenceRepo.Add(new DashboardWidgetPreference
                    {
                        UserId = userId,
                        WidgetKey = update.WidgetKey,
                        IsVisible = update.IsVisible,
                        SortOrder = update.SortOrder
                    });
                }
                else
                {
                    existing.IsVisible = update.IsVisible;
                    existing.SortOrder = update.SortOrder;
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }
    }
}
