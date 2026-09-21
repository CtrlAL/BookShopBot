using Amazon.S3;
using Amazon.S3.Model;
using BookShop.S3Tool.Configs;
using BookShop.S3Tool.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookShop.S3Tool.Implementations;

public sealed class S3Service : IS3Service
{
    private readonly IAmazonS3 _client;
    private readonly S3Config _config;
    private readonly ILogger<S3Service> _logger;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private bool _bucketEnsured;

    public S3Service(IAmazonS3 client, IOptions<S3Config> options, ILogger<S3Service> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _config = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CreateFile(Stream file, string folder, string fileName, string fileType)
    {
        try
        {
            await EnsureBucketAsync().ConfigureAwait(false);
            string key = BuildKey(folder, fileName);
            var request = new PutObjectRequest
            {
                BucketName = _config.Bucket,
                Key = key,
                ContentType = fileType,
                InputStream = file,
                AutoCloseStream = false,
            };
            await _client.PutObjectAsync(request).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 upload failed for key {Key}", BuildKey(folder, fileName));
            return false;
        }
    }

    public string GetPresignedUrl(string folder, string fileName, TimeSpan? lifetime = null)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _config.Bucket,
            Key = BuildKey(folder, fileName),
            Expires = DateTime.UtcNow.Add(lifetime ?? _config.PresignedLifetime),
            Verb = HttpVerb.GET,
        };
        return _client.GetPreSignedURL(request);
    }

    private static string BuildKey(string folder, string fileName) => $"{folder}/{fileName}";

    private async Task EnsureBucketAsync()
    {
        if (_bucketEnsured)
        {
            return;
        }

        await _ensureLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_bucketEnsured)
            {
                return;
            }

            var listResponse = await _client.ListBucketsAsync().ConfigureAwait(false);
            bool exists = listResponse.Buckets.Any(b =>
                string.Equals(b.BucketName, _config.Bucket, StringComparison.Ordinal));
            if (!exists)
            {
                await _client.PutBucketAsync(_config.Bucket).ConfigureAwait(false);
            }

            _bucketEnsured = true;
        }
        finally
        {
            _ensureLock.Release();
        }
    }
}