namespace Eventask.App.Services;

/// <summary>
/// Default implementation of IAuthService that manages authentication state
/// and coordinates with ITokenStorageService for persistence.
/// </summary>
public class AuthService : IAuthService
{
    private readonly ITokenStorageService _tokenStorage;

    private string? _token;
    private DateTimeOffset? _expiresAt;

    public AuthService(ITokenStorageService tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    public string? Token => _token;

    public DateTimeOffset? ExpiresAt => _expiresAt;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token) && _expiresAt > DateTimeOffset.UtcNow;

    public async Task SetTokenAsync(string token, DateTimeOffset expiresAt)
    {
        _token = token;
        _expiresAt = expiresAt;
        await _tokenStorage.SaveTokenAsync(token, expiresAt);
    }

    public async Task ClearTokenAsync()
    {
        _token = null;
        _expiresAt = null;
        await _tokenStorage.ClearTokenAsync();
    }

    public async Task<bool> TryLoadTokenAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        var expiresAt = await _tokenStorage.GetExpirationAsync();

        if (string.IsNullOrEmpty(token) || expiresAt == null || expiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        _token = token;
        _expiresAt = expiresAt;
        return true;
    }
}
