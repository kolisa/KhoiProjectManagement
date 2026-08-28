namespace KhoiProjectManagement.Application
{
    // The browser-supplied IFormFile.ContentType is empty for extensions the uploading OS/browser
    // doesn't recognize (common for less-common document and video formats), and generic
    // ("application/octet-stream") for others. Library stores that value verbatim as
    // LibraryFileVersion.ContentType and later hands it straight to Controller.File() for inline
    // viewing - an empty value breaks that response outright, and a generic one makes the browser
    // download instead of render inline. Resolve() fills in a real type from the file extension so
    // "View" works for documents and videos alike.
    public static class ContentTypeResolver
    {
        private static readonly Dictionary<string, string> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".svg"] = "image/svg+xml",
            [".mp4"] = "video/mp4",
            [".mov"] = "video/quicktime",
            [".webm"] = "video/webm",
            [".avi"] = "video/x-msvideo",
            [".mkv"] = "video/x-matroska",
            [".m4v"] = "video/mp4",
            [".mp3"] = "audio/mpeg",
            [".wav"] = "audio/wav",
        };

        public static string Resolve(string? browserContentType, string fileName)
        {
            if (!string.IsNullOrWhiteSpace(browserContentType) && browserContentType != "application/octet-stream")
                return browserContentType;

            var extension = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(extension) && ExtensionMap.TryGetValue(extension, out var mapped))
                return mapped;

            return "application/octet-stream";
        }
    }
}
