using Eventask.Domain.Entity.Calendars.ScheduleItems;

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
}