namespace Eventask.App.Services;

/// <summary>
/// Platform-agnostic interface for persisting application data locally.
/// Implementations should use platform-specific storage mechanisms.
/// </summary>
public interface ILocalStorageService
{
	/// <summary>
	/// Retrieves a stored value by key.
	/// </summary>
	/// <param name="key">The key to retrieve.</param>
	/// <returns>The stored value, or null if the key doesn't exist.</returns>
	Task<string?> GetAsync(string key);

	/// <summary>
	/// Saves a key-value pair.
	/// </summary>
	/// <param name="key">The key to store.</param>
	/// <param name="value">The value to store.</param>
	Task SetAsync(string key, string value);

	/// <summary>
	/// Removes a stored value by key.
	/// </summary>
	/// <param name="key">The key to remove.</param>
	Task RemoveAsync(string key);
}