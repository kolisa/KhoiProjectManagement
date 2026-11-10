using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class R2FileStorageTests
    {
        private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();

        private R2FileStorage CreateSut() => new(_s3, "project-management");

        [Fact]
        public async Task Save_PutsTheObjectInTheConfiguredBucket_WithContentType()
        {
            PutObjectRequest? sent = null;
            _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(r => sent = r), Arg.Any<CancellationToken>()).Returns(new PutObjectResponse());

            await CreateSut().SaveAsync("pm/1/a.png", new MemoryStream(new byte[] { 1, 2, 3 }), "image/png");

            Assert.Equal("project-management", sent!.BucketName);
            Assert.Equal("pm/1/a.png", sent.Key);
            Assert.Equal("image/png", sent.ContentType);
        }

        [Fact]
        public async Task Save_BuffersANonSeekableStream_SoTheLengthIsKnown()
        {
            PutObjectRequest? sent = null;
            long length = -1;
            _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(r => { sent = r; length = r.InputStream.Length; }), Arg.Any<CancellationToken>()).Returns(new PutObjectResponse());

            await CreateSut().SaveAsync("k", new NonSeekable(new byte[] { 1, 2, 3, 4 }), "");

            Assert.Equal(4, length);
            Assert.Equal("application/octet-stream", sent!.ContentType);
        }

        [Fact]
        public async Task OpenRead_WhenTheKeyIsMissing_ReturnsNull()
        {
            _s3.GetObjectAsync("project-management", "gone", Arg.Any<CancellationToken>())
                .Returns<GetObjectResponse>(_ => throw new AmazonS3Exception("nope") { StatusCode = HttpStatusCode.NotFound });

            Assert.Null(await CreateSut().OpenReadAsync("gone"));
        }

        [Fact]
        public async Task OpenRead_OtherErrors_Propagate()
        {
            _s3.GetObjectAsync("project-management", "k", Arg.Any<CancellationToken>())
                .Returns<GetObjectResponse>(_ => throw new AmazonS3Exception("denied") { StatusCode = HttpStatusCode.Forbidden });

            await Assert.ThrowsAsync<AmazonS3Exception>(() => CreateSut().OpenReadAsync("k"));
        }

        [Fact]
        public async Task Delete_RemovesTheKey()
        {
            await CreateSut().DeleteAsync("pm/1/a.png");
            await _s3.Received(1).DeleteObjectAsync("project-management", "pm/1/a.png", Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("BucketName")]
        [InlineData("AccessKeyId")]
        [InlineData("SecretAccessKey")]
        [InlineData("Endpoint")]
        public void Constructor_WithAMissingSetting_NamesIt(string missing)
        {
            var settings = new Dictionary<string, string?>
            {
                ["Storage:R2:Endpoint"] = "https://acct.r2.cloudflarestorage.com",
                ["Storage:R2:BucketName"] = "b",
                ["Storage:R2:AccessKeyId"] = "id",
                ["Storage:R2:SecretAccessKey"] = "secret",
            };
            settings.Remove($"Storage:R2:{missing}");
            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var ex = Assert.Throws<InvalidOperationException>(() => new R2FileStorage(config));
            Assert.Contains(missing, ex.Message);
        }

        [Fact]
        public void Constructor_WithFullSettings_Builds()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:R2:Endpoint"] = "https://acct.r2.cloudflarestorage.com",
                ["Storage:R2:BucketName"] = "b",
                ["Storage:R2:AccessKeyId"] = "id",
                ["Storage:R2:SecretAccessKey"] = "secret",
            }).Build();

            using var storage = new R2FileStorage(config);
            Assert.NotNull(storage);
        }

        private sealed class NonSeekable : MemoryStream
        {
            public NonSeekable(byte[] data) : base(data) { }
            public override bool CanSeek => false;
        }
    }
}
