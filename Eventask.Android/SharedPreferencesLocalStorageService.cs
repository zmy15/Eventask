using Android.Content;
using Eventask.App.Services;

namespace Eventask.Android;

/// <summary>
/// Android implementation of ILocalStorageService using SharedPreferences.
/// </summary>
public class SharedPreferencesLocalStorageService : ILocalStorageService
{
    private const string PreferencesName = "eventask_local";
    private readonly ISharedPreferences _preferences;

    public SharedPreferencesLocalStorageService(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("Failed to get SharedPreferences.");
    }

    public Task<string?> GetAsync(string key)
    {
        var value = _preferences.GetString(key, null);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value)
    {
        var editor = _preferences.Edit();
        editor?.PutString(key, value);
        editor?.Apply();
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        var editor = _preferences.Edit();
        editor?.Remove(key);
        editor?.Apply();
        return Task.CompletedTask;
    }
}
