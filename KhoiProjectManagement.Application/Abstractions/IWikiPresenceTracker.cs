namespace KhoiProjectManagement.Application
{
    public record WikiViewer(string ConnectionId, int UserId, string UserName);
    public record WikiEditLock(string ConnectionId, int UserId, string UserName);

    // In-memory, single-instance presence/edit-lock state for the Wiki's real-time collaboration
    // feature (WikiHub). Deliberately not persisted or distributed - same "no horizontal scaling today"
    // tradeoff already accepted for ISpacePermissionResolver's IMemoryCache snapshot, and this state is
    // inherently ephemeral (who's looking at a page right now), not business data worth keeping.
    public interface IWikiPresenceTracker
    {
        WikiViewer AddViewer(int pageId, string connectionId, int userId, string userName);
        void RemoveViewer(int pageId, string connectionId);
        List<WikiViewer> GetViewers(int pageId);

        // Grants the lock if it's free or already held by this connection; otherwise reports the
        // current holder without taking it.
        bool TryAcquireLock(int pageId, string connectionId, int userId, string userName);
        void ReleaseLock(int pageId, string connectionId);
        WikiEditLock? GetLock(int pageId);

        // Called on hub disconnect - removes this connection from every page it was viewing/editing and
        // returns the affected page ids so the caller can broadcast updated state to each.
        List<int> RemoveConnection(string connectionId);
    }
}
