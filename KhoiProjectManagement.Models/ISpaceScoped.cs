namespace KhoiProjectManagement.Models
{
    // Implemented by any entity that lives inside a Space and is authorized via that Space's
    // SpacePermission grants (VaultEntry now, WikiPage/file-library items later).
    public interface ISpaceScoped
    {
        int SpaceId { get; }
    }
}
