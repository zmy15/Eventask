using Android.Content;
using Eventask.App.Services;

namespace Eventask.Android;

/// <summary>
/// Android implementation of ITokenStorageService that stores tokens in SharedPreferences.
/// </summary>
public class SharedPreferencesTokenStorageService : ITokenStorageService
{
    private const string PreferencesName = "eventask_auth";
    private const string TokenKey = "access_token";
    private const string ExpirationKey = "expires_at";

    private readonly ISharedPreferences _preferences;

    public SharedPreferencesTokenStorageService(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("Failed to get SharedPreferences.");
    }

    public Task<string?> GetTokenAsync()
    {
        var token = _preferences.GetString(TokenKey, null);
        return Task.FromResult(token);
    }

    public Task<DateTimeOffset?> GetExpirationAsync()
    {
        var ticks = _preferences.GetLong(ExpirationKey, 0);
        if (ticks == 0)
        {
            return Task.FromResult<DateTimeOffset?>(null);
        }
        return Task.FromResult<DateTimeOffset?>(new DateTimeOffset(ticks, TimeSpan.Zero));
    }

    public Task SaveTokenAsync(string token, DateTimeOffset expiresAt)
    {
        var editor = _preferences.Edit();
        if (editor != null)
        {
            editor.PutString(TokenKey, token);
            editor.PutLong(ExpirationKey, expiresAt.UtcTicks);
            editor.Apply();
        }
        return Task.CompletedTask;
    }

    public Task ClearTokenAsync()
    {
        var editor = _preferences.Edit();
        if (editor != null)
        {
            editor.Remove(TokenKey);
            editor.Remove(ExpirationKey);
            editor.Apply();
        }
        return Task.CompletedTask;
    }
}
