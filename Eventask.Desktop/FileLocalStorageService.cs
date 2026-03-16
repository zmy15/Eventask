using System.Text.Json;
using Eventask.App.Services;

namespace Eventask.Desktop;

/// <summary>
/// Desktop implementation of ILocalStorageService that stores data in local files.
/// </summary>
public class FileLocalStorageService : ILocalStorageService
{
	private static readonly string StorageDirectory = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"Eventask",
		"storage"
	);

	public async Task<string?> GetAsync(string key)
	{
		var filePath = GetFilePath(key);
		
		if (!File.Exists(filePath))
		{
			return null;
		}

		try
		{
			return await File.ReadAllTextAsync(filePath);
		}
		catch (IOException)
		{
			// File access error, ignore and return null
			return null;
		}
	}

	public async Task SetAsync(string key, string value)
	{
		if (!Directory.Exists(StorageDirectory))
		{
			Directory.CreateDirectory(StorageDirectory);
		}

		var filePath = GetFilePath(key);
		await File.WriteAllTextAsync(filePath, value);
	}

	public Task RemoveAsync(string key)
	{
		var filePath = GetFilePath(key);
		
		if (File.Exists(filePath))
		{
			File.Delete(filePath);
		}
		
		return Task.CompletedTask;
	}

	private static string GetFilePath(string key)
	{
		// Sanitize the key to create a safe filename
		var safeFileName = string.Concat(key.Split(Path.GetInvalidFileNameChars()));
		return Path.Combine(StorageDirectory, $"{safeFileName}.txt");
	}
}