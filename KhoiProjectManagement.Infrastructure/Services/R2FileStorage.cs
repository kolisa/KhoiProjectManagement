using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using KhoiProjectManagement.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace KhoiProjectManagement.Infrastructure.Services
{
    // Cloudflare R2 (S3-compatible) implementation of IFileStorage. The bucket is private; files are only
    // ever served through the API (which checks project access first), never by public URL.
    //
    // Configuration (Storage:R2:*): Endpoint (https://<account>.r2.cloudflarestorage.com), BucketName,
    // AccessKeyId, SecretAccessKey. The two keys are secrets - supply them via environment variables
    // (Storage__R2__AccessKeyId / Storage__R2__SecretAccessKey) or user-secrets, never appsettings.json.
    public class R2FileStorage : IFileStorage, IDisposable
    {
        private readonly IAmazonS3 _client;
        private readonly string _bucket;

        public R2FileStorage(IConfiguration configuration)
        {
            var section = configuration.GetSection("Storage:R2");
            string Required(string key) =>
                section[key] is { Length: > 0 } value ? value : throw new InvalidOperationException($"Storage:R2:{key} is not configured.");

            _bucket = Required("BucketName");
            _client = new AmazonS3Client(
                new BasicAWSCredentials(Required("AccessKeyId"), Required("SecretAccessKey")),
                new AmazonS3Config
                {
                    ServiceURL = Required("Endpoint"),
                    // R2 ignores regions but the SDK still signs with one; "auto" is what Cloudflare documents.
                    AuthenticationRegion = "auto",
                    ForcePathStyle = true,
                    // Newer SDKs add a CRC32 trailer by default, which R2 rejects on streamed uploads.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                });
        }

        // For tests: inject a client.
        public R2FileStorage(IAmazonS3 client, string bucket)
        {
            _client = client;
            _bucket = bucket;
        }

        public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            // PutObject needs a known length for signing; buffer when the source cannot report one.
            Stream body = content;
            MemoryStream? buffered = null;
            if (!content.CanSeek)
            {
                buffered = new MemoryStream();
                await content.CopyToAsync(buffered, ct);
                buffered.Position = 0;
                body = buffered;
            }

            try
            {
                await _client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucket,
                    Key = key,
                    InputStream = body,
                    ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                    AutoCloseStream = false,
                    DisablePayloadSigning = true, // the endpoint is HTTPS; avoids hashing the whole body up front
                }, ct);
            }
            finally
            {
                buffered?.Dispose();
            }
        }

        public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
        {
            try
            {
                var response = await _client.GetObjectAsync(_bucket, key, ct);
                return new ResponseStream(response);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public Task DeleteAsync(string key, CancellationToken ct = default) =>
            _client.DeleteObjectAsync(_bucket, key, ct); // deleting a missing key is a no-op in S3/R2

        public void Dispose() => _client.Dispose();

        // Disposing the stream must also dispose the response, or the underlying HTTP connection leaks.
        private sealed class ResponseStream : Stream
        {
            private readonly GetObjectResponse _response;
            private readonly Stream _inner;

            public ResponseStream(GetObjectResponse response)
            {
                _response = response;
                _inner = response.ResponseStream;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _response.ContentLength;
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => _inner.ReadAsync(buffer, ct);
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _inner.ReadAsync(buffer, offset, count, ct);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing) _response.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
