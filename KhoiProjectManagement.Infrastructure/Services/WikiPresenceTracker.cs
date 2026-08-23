using KhoiProjectManagement.Application;
using System.Collections.Concurrent;

namespace KhoiProjectManagement.Infrastructure.Services
{
    public class WikiPresenceTracker : IWikiPresenceTracker
    {
        private class PageState
        {
            public readonly ConcurrentDictionary<string, WikiViewer> Viewers = new();
            public readonly object LockGate = new();
            public WikiEditLock? EditLock;
        }

        private readonly ConcurrentDictionary<int, PageState> _pages = new();
        private readonly ConcurrentDictionary<string, HashSet<int>> _connectionPages = new();

        private PageState GetOrAddPage(int pageId) => _pages.GetOrAdd(pageId, _ => new PageState());

        public WikiViewer AddViewer(int pageId, string connectionId, int userId, string userName)
        {
            var viewer = new WikiViewer(connectionId, userId, userName);
            GetOrAddPage(pageId).Viewers[connectionId] = viewer;

            var pages = _connectionPages.GetOrAdd(connectionId, _ => new HashSet<int>());
            lock (pages) { pages.Add(pageId); }

            return viewer;
        }

        public void RemoveViewer(int pageId, string connectionId)
        {
            if (_pages.TryGetValue(pageId, out var state))
            {
                state.Viewers.TryRemove(connectionId, out _);
                lock (state.LockGate)
                {
                    if (state.EditLock?.ConnectionId == connectionId)
                        state.EditLock = null;
                }
            }

            if (_connectionPages.TryGetValue(connectionId, out var pages))
            {
                lock (pages) { pages.Remove(pageId); }
            }
        }

        public List<WikiViewer> GetViewers(int pageId) =>
            _pages.TryGetValue(pageId, out var state) ? state.Viewers.Values.ToList() : new List<WikiViewer>();

        public bool TryAcquireLock(int pageId, string connectionId, int userId, string userName)
        {
            var state = GetOrAddPage(pageId);
            lock (state.LockGate)
            {
                if (state.EditLock == null || state.EditLock.ConnectionId == connectionId)
                {
                    state.EditLock = new WikiEditLock(connectionId, userId, userName);
                    return true;
                }
                return false;
            }
        }

        public void ReleaseLock(int pageId, string connectionId)
        {
            if (_pages.TryGetValue(pageId, out var state))
            {
                lock (state.LockGate)
                {
                    if (state.EditLock?.ConnectionId == connectionId)
                        state.EditLock = null;
                }
            }
        }

        public WikiEditLock? GetLock(int pageId) =>
            _pages.TryGetValue(pageId, out var state) ? state.EditLock : null;

        public List<int> RemoveConnection(string connectionId)
        {
            var affected = new List<int>();
            if (_connectionPages.TryRemove(connectionId, out var pages))
            {
                List<int> pageIds;
                lock (pages) { pageIds = pages.ToList(); }

                foreach (var pageId in pageIds)
                {
                    RemoveViewer(pageId, connectionId);
                    affected.Add(pageId);
                }
            }
            return affected;
        }
    }
}
