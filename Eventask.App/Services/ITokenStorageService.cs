namespace Eventask.App.Services;

/// <summary>
/// Platform-agnostic interface for persisting authentication tokens.
/// Implementations should use platform-specific secure storage mechanisms.
/// </summary>
public interface ITokenStorageService
{
    /// <summary>
    /// Retrieves the stored access token.
    /// </summary>
    /// <returns>The stored token, or null if no token is stored.</returns>
    Task<string?> GetTokenAsync();

    /// <summary>
    /// Retrieves the stored token expiration date.
    /// </summary>
    /// <returns>The expiration date, or null if no token is stored.</returns>
    Task<DateTimeOffset?> GetExpirationAsync();

    /// <summary>
    /// Saves the access token and its expiration date.
    /// </summary>
    /// <param name="token">The access token to store.</param>
    /// <param name="expiresAt">The token's expiration date.</param>
    Task SaveTokenAsync(string token, DateTimeOffset expiresAt);

    /// <summary>
    /// Clears the stored token and expiration date.
    /// </summary>
    Task ClearTokenAsync();
}
