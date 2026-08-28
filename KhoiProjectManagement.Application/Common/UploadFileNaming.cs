namespace KhoiProjectManagement.Application
{
    // Shared by every feature that saves an IFormFile to disk (Library, Finance/Invoice, Ideas,
    // Projects/Attachment) - factored out after the same on-disk-filename construction was found
    // duplicated across all four, each using the client-supplied IFormFile.FileName unsanitized. A
    // GUID prefix does not neutralize a "../" sequence appearing later in that name, so without
    // Path.GetFileName() stripping any directory component first, a crafted filename could write
    // outside the intended upload directory.
    public static class UploadFileNaming
    {
        public static string BuildStoredFileName(string? clientFileName) =>
            $"{Guid.NewGuid()}_{Path.GetFileName(clientFileName) ?? "file"}";
    }
}
