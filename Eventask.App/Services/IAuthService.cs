namespace Eventask.App.Services;

/// <summary>
/// Service for managing authentication state and providing tokens for API requests.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Gets the current access token, or null if not authenticated.
    /// </summary>
    string? Token { get; }

    /// <summary>
    /// Gets the token expiration date, or null if not authenticated.
    /// </summary>
    DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Gets whether the user is currently authenticated with a valid (non-expired) token.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Sets the authentication token and expiration, and persists it using the token storage service.
    /// </summary>
    /// <param name="token">The access token.</param>
    /// <param name="expiresAt">The token's expiration date.</param>
    Task SetTokenAsync(string token, DateTimeOffset expiresAt);

    /// <summary>
    /// Clears the current authentication token and removes it from storage.
    /// </summary>
    Task ClearTokenAsync();

    /// <summary>
    /// Loads the token from persistent storage (called on app startup).
    /// </summary>
    /// <returns>True if a valid token was loaded, false otherwise.</returns>
    Task<bool> TryLoadTokenAsync();
}
