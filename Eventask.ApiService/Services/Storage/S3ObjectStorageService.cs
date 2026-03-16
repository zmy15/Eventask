using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Eventask.ApiService.Services.Storage;

public sealed class S3ObjectStorageService : IObjectStorageService, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly ObjectStorageOptions _options;
    private bool _disposed;

    public S3ObjectStorageService (IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;
        if ( String.IsNullOrWhiteSpace(_options.Endpoint) )
            throw new InvalidOperationException("Object storage endpoint is not configured.");
        if ( String.IsNullOrWhiteSpace(_options.Bucket) )
            throw new InvalidOperationException("Object storage bucket is not configured.");
        if ( String.IsNullOrWhiteSpace(_options.AccessKey) || String.IsNullOrWhiteSpace(_options.SecretKey) )
            throw new InvalidOperationException("Object storage credentials are not configured.");

        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = _options.ForcePathStyle
        };

        var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
        _client = new AmazonS3Client(credentials, config);
    }

    public async Task<string> UploadAsync (string objectKey, IFormFile file,
        CancellationToken cancellationToken = default)
    {
        // Ensure stream can be read
        await using var stream = file.OpenReadStream();
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            InputStream = stream,
            ContentType = file.ContentType,
            AutoCloseStream = true,
            DisablePayloadSigning = true
        };

        await _client.PutObjectAsync(request, cancellationToken);
        return objectKey;
    }

    public async Task<(Stream Stream, string ContentType, long Size)> DownloadAsync (string objectKey,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey
        }, cancellationToken);

        var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return (memory, response.Headers.ContentType ?? "application/octet-stream", response.Headers.ContentLength);
    }

    public async Task DeleteAsync (string objectKey, CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey
        }, cancellationToken);
    }

    public void Dispose ( )
    {
        if ( _disposed )
            return;
        _disposed = true;
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}