namespace KhoiProjectManagement.Application
{
    // Shared by every feature that saves an IFormFile to disk (Library, Finance/Invoice, Ideas,
    // Projects/Attachment) - factored out after the same on-disk-filename construction was found
    // duplicated across all four, each using the client-supplied IFormFile.FileName unsanitized. A
    // GUID prefix does not neutralize a "../" sequence appearing later in that name, so without
    // Path.GetFileName() stripping any directory component first, a crafted filename could write
    // outside the intended upload directory.
    //
    // Both separators are stripped on every OS. Path.GetFileName only treats the OS's own separators as
    // separators, so on the Linux server a name like "..\\..\\evil.png" - a Windows-style path from the
    // client - was left intact; normalising backslashes first makes the result identical on Windows and Linux.
    public static class UploadFileNaming
    {
        // The bare file name a client sent, with any directory part (either separator style) removed.
        // Empty when there is nothing usable.
        public static string SanitizeFileName(string? clientFileName) =>
            Path.GetFileName((clientFileName ?? string.Empty).Replace('\\', '/'));

        public static string BuildStoredFileName(string? clientFileName)
        {
            var name = SanitizeFileName(clientFileName);
            return $"{Guid.NewGuid()}_{(name.Length > 0 ? name : "file")}";
        }
    }
}
