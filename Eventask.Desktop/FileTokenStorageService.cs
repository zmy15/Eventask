using System.Text.Json;
using Eventask.App.Services;

namespace Eventask.Desktop;

/// <summary>
/// Desktop implementation of ITokenStorageService that stores tokens in a local file.
/// </summary>
public class FileTokenStorageService : ITokenStorageService
{
    private static readonly string TokenFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Eventask",
        "auth_token.json"
    );

    private readonly record struct StoredToken(string Token, DateTimeOffset ExpiresAt);

    public async Task<string?> GetTokenAsync()
    {
        var storedToken = await LoadTokenDataAsync();
        return storedToken?.Token;
    }

    public async Task<DateTimeOffset?> GetExpirationAsync()
    {
        var storedToken = await LoadTokenDataAsync();
        return storedToken?.ExpiresAt;
    }

    public async Task SaveTokenAsync(string token, DateTimeOffset expiresAt)
    {
        var directory = Path.GetDirectoryName(TokenFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var storedToken = new StoredToken(token, expiresAt);
        var json = JsonSerializer.Serialize(storedToken);
        await File.WriteAllTextAsync(TokenFilePath, json);
    }

    public Task ClearTokenAsync()
    {
        if (File.Exists(TokenFilePath))
        {
            File.Delete(TokenFilePath);
        }
        return Task.CompletedTask;
    }

    private static async Task<StoredToken?> LoadTokenDataAsync()
    {
        if (!File.Exists(TokenFilePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(TokenFilePath);
            return JsonSerializer.Deserialize<StoredToken>(json);
        }
        catch (JsonException)
        {
            // Token file is corrupted, ignore and return null
            return null;
        }
        catch (IOException)
        {
            // File access error, ignore and return null
            return null;
        }
    }
}
