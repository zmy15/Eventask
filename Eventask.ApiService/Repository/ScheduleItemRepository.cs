using Eventask.Domain.Entity.Calendars.ScheduleItems;
using Microsoft.EntityFrameworkCore;

namespace Eventask.ApiService.Repository;

public class ScheduleItemRepository(EventaskContext db) : IScheduleItemRepository
{
    /// <inheritdoc/>
    /// <summary>
    /// Adds a schedule item to the tracking context. Changes will be persisted when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    public Task AddNewItemTrackingAsync(ScheduleItem item)
    {
        db.ScheduleItems.Add(item);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns all schedule items (including deleted tombstone records) that were updated
    /// after the specified timestamp, filtered by the given calendar IDs,
    /// ordered by UpdatedAt, with pagination. Used by Sync Pull.
    /// Does NOT filter out IsDeleted — callers receive tombstone records for sync.
    /// </summary>
    public async Task<IReadOnlyList<ScheduleItem>> ListChangedSinceAsync(
        IReadOnlyList<Guid> calendarIds, DateTimeOffset changedSince, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var items = await db.ScheduleItems
            .Include(i => i.Attachments)
            .Include(i => i.Reminders)
            .Where(i => calendarIds.Contains(i.CalendarId) && i.UpdatedAt > changedSince)
            .OrderBy(i => i.UpdatedAt)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return items.AsReadOnly();
    }
}
    