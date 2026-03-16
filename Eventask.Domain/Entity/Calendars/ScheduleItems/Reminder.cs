using Eventask.Domain.Contracts;

namespace Eventask.Domain.Entity.Calendars.ScheduleItems;

public class Reminder : ISynchronizableEntity
{
    public Guid Id { get; private set; }

    public Guid ScheduleItemId { get; private set; }

    public int OffsetMinutes { get; private set; }

    public int Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ScheduleItem? ScheduleItem { get; set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private Reminder() { }

    /// <summary>
    /// Factory method to create a new Reminder.
    /// </summary>
    public static Reminder Create(Guid scheduleItemId, int offsetMinutes)
    {
        if (offsetMinutes < 0)
            throw new ArgumentException("Offset minutes cannot be negative.", nameof(offsetMinutes));

        var now = DateTimeOffset.UtcNow;
        return new Reminder
        {
            Id = Guid.NewGuid(),
            ScheduleItemId = scheduleItemId,
            OffsetMinutes = offsetMinutes,
            Version = 0,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    /// <summary>
    /// Marks the entity as modified. Updates UpdatedAt only.
    /// Version is incremented server-side by the repository after successful persistence.
    /// </summary>
    private void MarkModified()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateOffset(int newOffsetMinutes)
    {
        if (newOffsetMinutes < 0)
            throw new ArgumentException("Offset minutes cannot be negative.", nameof(newOffsetMinutes));
        OffsetMinutes = newOffsetMinutes;
        MarkModified();
    }

    public void MarkDeleted(DateTimeOffset deletedAt)
    {
        if (IsDeleted)
            return;
        IsDeleted = true;
        DeletedAt = deletedAt;
        MarkModified();
    }

    public void Restore()
    {
        if (!IsDeleted)
            return;
        IsDeleted = false;
        DeletedAt = null;
        MarkModified();
    }
}
