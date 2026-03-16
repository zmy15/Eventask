using Microsoft.AspNetCore.Http;

namespace Eventask.ApiService.Services.Storage;

public interface IObjectStorageService
{
    /// <summary>
    /// Upload a stream to the storage backend and return the object key.
    /// </summary>
    Task<string> UploadAsync(string objectKey, IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download an object as stream with content type.
    /// </summary>
    Task<(Stream Stream, string ContentType, long Size)> DownloadAsync(string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an object from storage.
    /// </summary>
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}