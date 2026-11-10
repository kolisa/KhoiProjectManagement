using KhoiProjectManagement.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace KhoiProjectManagement.Infrastructure.Services
{
    // Stores files under FileUpload:UploadPath (the same root the classic attachment feature uses).
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _root;

        public LocalFileStorage(IConfiguration configuration)
        {
            _root = Path.GetFullPath(configuration["FileUpload:UploadPath"] ?? "wwwroot/uploads");
        }

        // Keys are generated server-side, but resolve defensively so a bad key can never escape the root.
        private string Resolve(string key)
        {
            var path = Path.GetFullPath(Path.Combine(_root, key));
            if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid storage key.");
            return path;
        }

        public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            var path = Resolve(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var file = new FileStream(path, FileMode.Create);
            await content.CopyToAsync(file, ct);
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
        {
            var path = Resolve(key);
            return Task.FromResult<Stream?>(File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null);
        }

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            var path = Resolve(key);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }
    }
}
