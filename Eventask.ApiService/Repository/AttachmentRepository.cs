using Eventask.Domain.Entity.Calendars.ScheduleItems;
using Microsoft.EntityFrameworkCore;

namespace Eventask.ApiService.Repository;

public class AttachmentRepository(EventaskContext db) : IAttachmentRepository
{
    /// <summary>
    /// Adds an attachment to the tracking context. Changes will be persisted when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    public Task AddNewItemTrackingAsync(Attachment item)
    {
        db.Add(item);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns all attachments (including deleted tombstone records) that were updated
    /// after the specified timestamp, filtered by the given schedule item IDs,
    /// ordered by UpdatedAt, with pagination. Used by Sync Pull.
    /// Does NOT filter out IsDeleted — callers receive tombstone records for sync.
    /// Uses db.Set&lt;Attachment&gt;() because Attachment is an owned entity type
    /// without a top-level DbSet on EventaskContext.
    /// </summary>
    public async Task<IReadOnlyList<Attachment>> ListChangedSinceAsync(
        IReadOnlyList<Guid> scheduleItemIds, DateTimeOffset changedSince, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var attachments = await db.Set<Attachment>()
            .Where(a => scheduleItemIds.Contains(a.ScheduleItemId) && a.UpdatedAt > changedSince)
            .OrderBy(a => a.UpdatedAt)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return attachments.AsReadOnly();
    }
}
    