using Amazon.S3;
using Amazon.S3.Model;
using BookShop.S3Tool.Configs;
using BookShop.S3Tool.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BookShopBot.Tests.S3;

public sealed class S3ServiceTests
{
    private static S3Service BuildService(
        Mock<IAmazonS3> client,
        S3Config? config = null,
        bool bucketExists = true)
    {
        client.Setup(c => c.ListBucketsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(bucketExists
                ? new ListBucketsResponse
                {
                    Buckets = new List<S3Bucket> { new S3Bucket { BucketName = "bookshop" } },
                }
                : new ListBucketsResponse());
        var options = Options.Create(config ?? new S3Config());
        return new S3Service(client.Object, options, NullLogger<S3Service>.Instance);
    }

    [Fact]
    public async Task CreateFile_calls_PutObject_with_expected_key_and_content_type()
    {
        var client = new Mock<IAmazonS3>();
        var service = BuildService(client);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await service.CreateFile(stream, "BookShopUploads", "book.pdf", "application/pdf");

        Assert.True(result);
        client.Verify(c => c.PutObjectAsync(
            It.Is<PutObjectRequest>(r =>
                r.BucketName == "bookshop" &&
                r.Key == "BookShopUploads/book.pdf" &&
                r.ContentType == "application/pdf"),
            default), Times.Once);
    }

    [Fact]
    public async Task CreateFile_creates_bucket_when_missing()
    {
        var client = new Mock<IAmazonS3>();
        var service = BuildService(client, bucketExists: false);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await service.CreateFile(stream, "f", "n.txt", "text/plain");

        client.Verify(c => c.PutBucketAsync("bookshop", default), Times.Once);
        client.Verify(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
    }

    [Fact]
    public void GetPresignedUrl_uses_default_lifetime_when_null()
    {
        var client = new Mock<IAmazonS3>();
        client.Setup(c => c.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns((GetPreSignedUrlRequest r) => r.Key);
        var service = BuildService(client);

        var url = service.GetPresignedUrl("BookShopUploads", "book.pdf");

        Assert.Contains("BookShopUploads/book.pdf", url);
        client.Verify(c => c.GetPreSignedURL(It.Is<GetPreSignedUrlRequest>(r =>
            r.Expires - DateTime.UtcNow >= TimeSpan.FromDays(364))), Times.Once);
    }

    [Fact]
    public void GetPresignedUrl_uses_explicit_lifetime()
    {
        var client = new Mock<IAmazonS3>();
        client.Setup(c => c.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("http://presigned");
        var service = BuildService(client);

        var url = service.GetPresignedUrl("f", "n.png", TimeSpan.FromDays(2));

        Assert.Equal("http://presigned", url);
        client.Verify(c => c.GetPreSignedURL(It.Is<GetPreSignedUrlRequest>(r =>
            r.Expires - DateTime.UtcNow <= TimeSpan.FromDays(3))), Times.Once);
    }

    [Fact]
    public async Task CreateFile_returns_false_on_amazon_exception()
    {
        var client = new Mock<IAmazonS3>();
        client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ThrowsAsync(new AmazonS3Exception("boom"));
        var service = BuildService(client);

        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await service.CreateFile(stream, "f", "n", "text/plain");

        Assert.False(result);
    }
}