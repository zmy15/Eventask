namespace Eventask.ApiService.Services.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>
    /// The S3-compatible service endpoint (e.g., https://s3.amazonaws.com or http://localhost:9000 for MinIO).
    /// </summary>
    public string Endpoint { get; init; } = String.Empty;

    /// <summary>
    /// AWS region or compatibility region name (e.g., us-east-1).
    /// </summary>
    public string Region { get; init; } = "us-east-1";

    /// <summary>
    /// Target bucket name. The bucket must be pre-created.
    /// </summary>
    public string Bucket { get; init; } = String.Empty;

    /// <summary>
    /// Access key for the S3-compatible service.
    /// </summary>
    public string AccessKey { get; init; } = String.Empty;

    /// <summary>
    /// Secret key for the S3-compatible service.
    /// </summary>
    public string SecretKey { get; init; } = String.Empty;

    /// <summary>
    /// Use path-style access (recommended for many S3-compatible endpoints such as MinIO).
    /// </summary>
    public bool ForcePathStyle { get; init; } = true;
}