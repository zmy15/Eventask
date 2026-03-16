namespace Eventask.App.Services;

/// <summary>
/// Service for managing the currently selected calendar state.
/// </summary>
public interface ICalendarStateService
{
	/// <summary>
	/// Gets the currently selected calendar ID.
	/// </summary>
	Guid CurrentCalendarId { get; }

	/// <summary>
	/// Gets the currently selected calendar name.
	/// </summary>
	string? CurrentCalendarName { get; }

	/// <summary>
	/// Sets the current calendar ID and name, and persists it to storage.
	/// </summary>
	/// <param name="calendarId">The calendar ID to set as current.</param>
	/// <param name="calendarName">The calendar name to set as current.</param>
	Task SetCurrentCalendarAsync(Guid calendarId, string? calendarName = null);

	/// <summary>
	/// Clears the current calendar selection.
	/// </summary>
	Task ClearCurrentCalendarAsync();

	/// <summary>
	/// Loads the current calendar ID from persistent storage (called on app startup).
	/// </summary>
	/// <returns>True if a valid calendar ID was loaded, false otherwise.</returns>
	Task<bool> TryLoadCurrentCalendarAsync();

	/// <summary>
	/// Ensures a valid calendar is selected by loading from storage or fetching the first available calendar.
	/// </summary>
	/// <returns>The current calendar ID, or Guid.Empty if no calendar is available.</returns>
	Task<Guid> EnsureCalendarSelectedAsync();
}