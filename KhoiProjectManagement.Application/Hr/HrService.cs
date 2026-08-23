using System.Security.Claims;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class HrService : IHrService
    {
        private readonly IRepository<OnboardingTemplate> _templateRepo;
        private readonly IRepository<OnboardingTemplateItem> _templateItemRepo;
        private readonly IRepository<OnboardingChecklist> _checklistRepo;
        private readonly IUnitOfWork _unitOfWork;

        public HrService(
            IRepository<OnboardingTemplate> templateRepo,
            IRepository<OnboardingTemplateItem> templateItemRepo,
            IRepository<OnboardingChecklist> checklistRepo,
            IUnitOfWork unitOfWork)
        {
            _templateRepo = templateRepo;
            _templateItemRepo = templateItemRepo;
            _checklistRepo = checklistRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<OnboardingTemplateDto>> GetTemplatesAsync()
        {
            var templates = await _templateRepo.Query()
                .Include(t => t.Items)
                .ToListAsync();

            return templates.Select(MapTemplate).ToList();
        }

        public async Task<OnboardingTemplateDto> CreateTemplateAsync(CreateOnboardingTemplateDto dto)
        {
            var template = new OnboardingTemplate
            {
                Name = dto.Name,
                Items = dto.ItemTitles.Select((title, index) => new OnboardingTemplateItem
                {
                    Title = title,
                    SortOrder = index
                }).ToList()
            };

            _templateRepo.Add(template);
            await _unitOfWork.SaveChangesAsync();

            return MapTemplate(template);
        }

        public async Task<bool> UpdateTemplateAsync(int id, UpdateOnboardingTemplateDto dto)
        {
            var template = await _templateRepo.Query()
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (template == null)
                return false;

            template.Name = dto.Name;
            template.IsActive = dto.IsActive;

            // Existing checklists already copied item titles at creation time (per-entity, not
            // live-linked) - full-replacing the template's own items here never touches them.
            _templateItemRepo.RemoveRange(template.Items);
            template.Items = dto.ItemTitles.Select((title, index) => new OnboardingTemplateItem
            {
                TemplateId = template.Id,
                Title = title,
                SortOrder = index
            }).ToList();

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<List<OnboardingChecklistDto>> GetChecklistsAsync(int? userId, ClaimsPrincipal caller)
        {
            var callerId = GetUserId(caller);
            var targetUserId = userId ?? callerId;

            if (targetUserId != callerId && !caller.HasClaim("permission", "hr.view") && !caller.HasClaim("permission", "hr.manage"))
                throw new UnauthorizedAccessException("Caller lacks hr.view access to another user's checklists.");

            var checklists = await _checklistRepo.Query()
                .Include(c => c.User)
                .Include(c => c.Template)
                .Include(c => c.Items).ThenInclude(i => i.Completer)
                .Where(c => c.UserId == targetUserId)
                .ToListAsync();

            return checklists.Select(MapChecklist).ToList();
        }

        public async Task<OnboardingChecklistDto?> GetChecklistByIdAsync(int id, ClaimsPrincipal caller)
        {
            var checklist = await _checklistRepo.Query()
                .Include(c => c.User)
                .Include(c => c.Template)
                .Include(c => c.Items).ThenInclude(i => i.Completer)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (checklist == null)
                return null;

            var callerId = GetUserId(caller);
            if (checklist.UserId != callerId && !caller.HasClaim("permission", "hr.view") && !caller.HasClaim("permission", "hr.manage"))
                throw new UnauthorizedAccessException($"Caller lacks access to checklist {id}.");

            return MapChecklist(checklist);
        }

        public async Task<OnboardingChecklistDto> CreateChecklistAsync(CreateOnboardingChecklistDto dto)
        {
            var template = await _templateRepo.Query()
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == dto.TemplateId)
                ?? throw new InvalidOperationException("Template not found.");

            var checklist = new OnboardingChecklist
            {
                UserId = dto.UserId,
                TemplateId = dto.TemplateId,
                Items = template.Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new OnboardingChecklistItem { Title = i.Title })
                    .ToList()
            };

            _checklistRepo.Add(checklist);
            await _unitOfWork.SaveChangesAsync();

            var saved = await _checklistRepo.Query()
                .Include(c => c.User)
                .Include(c => c.Template)
                .Include(c => c.Items).ThenInclude(i => i.Completer)
                .FirstAsync(c => c.Id == checklist.Id);

            return MapChecklist(saved);
        }

        public async Task<bool> UpdateChecklistItemAsync(int checklistId, int itemId, UpdateChecklistItemDto dto, ClaimsPrincipal caller)
        {
            var checklist = await _checklistRepo.Query()
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == checklistId);
            if (checklist == null)
                return false;

            var item = checklist.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
                return false;

            var callerId = GetUserId(caller);
            if (checklist.UserId != callerId && !caller.HasClaim("permission", "hr.manage"))
                throw new UnauthorizedAccessException($"Caller lacks access to modify checklist {checklistId}.");

            item.IsComplete = dto.IsComplete;
            item.Notes = dto.Notes;
            if (dto.IsComplete)
            {
                item.CompletedAt = DateTime.UtcNow;
                item.CompletedBy = callerId;
            }
            else
            {
                item.CompletedAt = null;
                item.CompletedBy = null;
            }

            if (checklist.Items.All(i => i.IsComplete))
                checklist.CompletedAt ??= DateTime.UtcNow;
            else
                checklist.CompletedAt = null;

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        private static OnboardingTemplateDto MapTemplate(OnboardingTemplate t) => new()
        {
            Id = t.Id,
            Name = t.Name,
            IsActive = t.IsActive,
            Items = t.Items.OrderBy(i => i.SortOrder).Select(i => new OnboardingTemplateItemDto
            {
                Id = i.Id,
                Title = i.Title,
                SortOrder = i.SortOrder
            }).ToList()
        };

        private static OnboardingChecklistDto MapChecklist(OnboardingChecklist c) => new()
        {
            Id = c.Id,
            UserId = c.UserId,
            UserName = c.User?.Name ?? "Unknown",
            TemplateId = c.TemplateId,
            TemplateName = c.Template?.Name ?? "Unknown",
            CreatedAt = c.CreatedAt,
            CompletedAt = c.CompletedAt,
            Items = c.Items.Select(i => new OnboardingChecklistItemDto
            {
                Id = i.Id,
                Title = i.Title,
                IsComplete = i.IsComplete,
                CompletedAt = i.CompletedAt,
                CompletedByName = i.Completer?.Name,
                Notes = i.Notes
            }).ToList()
        };

        private static int GetUserId(ClaimsPrincipal caller)
        {
            var claim = caller.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Caller has no NameIdentifier claim.");
            return int.Parse(claim.Value);
        }
    }
}
