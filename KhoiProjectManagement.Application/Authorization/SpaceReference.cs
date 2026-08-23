using KhoiProjectManagement.Domain;

namespace KhoiProjectManagement.Application.Authorization
{
    // A lightweight ISpaceScoped shim for authorizing directly against a Space id itself (e.g. managing
    // the Space's own settings/permissions), rather than against an item that lives inside a Space.
    public class SpaceReference : ISpaceScoped
    {
        public SpaceReference(int spaceId) => SpaceId = spaceId;
        public int SpaceId { get; }
    }
}
