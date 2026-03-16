using Eventask.Domain.Entity.Calendars.ScheduleItems;

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
}