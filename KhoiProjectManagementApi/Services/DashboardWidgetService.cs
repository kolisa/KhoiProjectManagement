using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class DashboardWidgetService : IDashboardWidgetService
    {
        private readonly ProjectManagementContext _context;

        public DashboardWidgetService(ProjectManagementContext context)
        {
            _context = context;
        }

        public async Task<List<DashboardWidgetCatalogEntryDto>> GetCatalogAsync()
        {
            var allowlist = await _context.DashboardWidgetAllowlists
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

                var existing = await _context.DashboardWidgetAllowlists
                    .FirstOrDefaultAsync(a => a.WidgetKey == update.WidgetKey);

                if (existing == null)
                {
                    _context.DashboardWidgetAllowlists.Add(new DashboardWidgetAllowlist
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

            await _context.SaveChangesAsync();
        }

        public async Task<List<DashboardWidgetPreferenceDto>> GetMyPreferencesAsync(int userId)
        {
            var allowlist = await _context.DashboardWidgetAllowlists
                .ToDictionaryAsync(a => a.WidgetKey, a => a.IsEnabled);

            var userPrefs = await _context.DashboardWidgetPreferences
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

                var existing = await _context.DashboardWidgetPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.WidgetKey == update.WidgetKey);

                if (existing == null)
                {
                    _context.DashboardWidgetPreferences.Add(new DashboardWidgetPreference
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

            await _context.SaveChangesAsync();
        }
    }
}
