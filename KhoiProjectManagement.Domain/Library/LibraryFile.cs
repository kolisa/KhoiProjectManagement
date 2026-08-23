namespace KhoiProjectManagement.Domain
{
    // A file library "folder" is just a Space (SpaceType.Generic or a dedicated LibraryFolder type) -
    // no new container concept, same as vault categories and wiki spaces. Files are stored on local
    // disk (wwwroot/library-files), not the vault's encrypted-DB-column approach - files aren't
    // secrets, and re-encrypting arbitrary binaries through Data Protection would be slow and
    // unnecessary. StoredPath is always what's used to read from disk - see LibraryController, which
    // deliberately never repeats the AttachmentsController.DownloadFile bug of using the display name.
    public class LibraryFile : BaseEntity, ISpaceScoped
    {
        public int SpaceId { get; set; }
        public virtual Space Space { get; set; } = null!;

        public string FileName { get; set; } = string.Empty;

        public int CreatedBy { get; set; }
        public virtual User Creator { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public virtual ICollection<LibraryFileVersion> Versions { get; set; } = new List<LibraryFileVersion>();
    }
}
