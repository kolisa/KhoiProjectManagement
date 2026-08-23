using System.Security.Claims;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KhoiProjectManagementApi.Hubs
{
    // Presence + soft edit-lock for wiki pages - not full simultaneous co-editing. At most one caller
    // holds the edit lock on a page at a time; everyone connected to the page sees who's viewing and
    // who (if anyone) is currently editing. Every method re-checks access via IWikiService rather than
    // trusting the client, matching the "re-check inside the service, not just an attribute" pattern
    // used everywhere else Space-scoped access is enforced.
    [Authorize]
    public class WikiHub : Hub
    {
        private readonly IWikiService _wikiService;
        private readonly IWikiPresenceTracker _presence;

        public WikiHub(IWikiService wikiService, IWikiPresenceTracker presence)
        {
            _wikiService = wikiService;
            _presence = presence;
        }

        private static string GroupName(int pageId) => $"wikipage-{pageId}";

        private int GetUserId() => int.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private string GetUserName() => Context.User!.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

        public async Task JoinPage(int pageId)
        {
            var level = await _wikiService.GetMyLevelForPageAsync(pageId, Context.User!);
            if (level == null)
                throw new HubException("You don't have access to this page.");

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(pageId));
            _presence.AddViewer(pageId, Context.ConnectionId, GetUserId(), GetUserName());
            await BroadcastPresence(pageId);
            await BroadcastLock(pageId);
        }

        public async Task<bool> StartEditing(int pageId)
        {
            var level = await _wikiService.GetMyLevelForPageAsync(pageId, Context.User!);
            if (level == null || level.Value < PermissionLevel.Write)
                throw new HubException("You don't have edit access to this page.");

            var acquired = _presence.TryAcquireLock(pageId, Context.ConnectionId, GetUserId(), GetUserName());
            await BroadcastLock(pageId);
            return acquired;
        }

        public async Task StopEditing(int pageId)
        {
            _presence.ReleaseLock(pageId, Context.ConnectionId);
            await BroadcastLock(pageId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var affectedPages = _presence.RemoveConnection(Context.ConnectionId);
            foreach (var pageId in affectedPages)
            {
                await BroadcastPresence(pageId);
                await BroadcastLock(pageId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        private async Task BroadcastPresence(int pageId)
        {
            var viewers = _presence.GetViewers(pageId)
                .Select(v => new { v.UserId, v.UserName })
                .DistinctBy(v => v.UserId)
                .ToList();
            await Clients.Group(GroupName(pageId)).SendAsync("PresenceUpdated", pageId, viewers);
        }

        private async Task BroadcastLock(int pageId)
        {
            var editLock = _presence.GetLock(pageId);
            await Clients.Group(GroupName(pageId)).SendAsync("EditLockChanged", pageId,
                editLock == null ? null : new { editLock.UserId, editLock.UserName });
        }
    }
}
