namespace KhoiProjectManagement.Application.Abstractions
{
    // Port for binary file storage. Local disk today; an object store (Cloudflare R2 via its
    // S3-compatible API) is a drop-in Infrastructure implementation, since callers only ever hold the
    // opaque `key` returned from SaveAsync.
    public interface IFileStorage
    {
        Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);
        // Null when the object does not exist.
        Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
        Task DeleteAsync(string key, CancellationToken ct = default);
    }
}
