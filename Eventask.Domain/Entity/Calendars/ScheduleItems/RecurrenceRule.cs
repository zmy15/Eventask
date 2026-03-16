using Eventask.Domain.Contracts;

namespace Eventask.Domain.Entity.Calendars.ScheduleItems;

public class RecurrenceRule : ISynchronizableEntity
{
    public Guid Id { get; private set; }

    public Guid ScheduleItemId { get; private set; }

    public RecurrenceFrequency Freq { get; private set; }

    public int Interval { get; private set; } = 1;

    public string? ByDay { get; private set; }

    public DateTimeOffset? Until { get; private set; }

    public int? Count { get; private set; }

    public int Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ScheduleEvent? ScheduleItem { get; set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private RecurrenceRule() { }

    /// <summary>
    /// Factory method to create a new RecurrenceRule.
    /// </summary>
    public static RecurrenceRule Create(
        Guid scheduleItemId,
        RecurrenceFrequency freq,
        int interval = 1,
        string? byDay = null,
        DateTimeOffset? until = null,
        int? count = null)
    {
        if (interval < 1)
            throw new ArgumentException("Interval must be at least 1.", nameof(interval));

        var now = DateTimeOffset.UtcNow;
        return new RecurrenceRule
        {
            Id = Guid.NewGuid(),
            ScheduleItemId = scheduleItemId,
            Freq = freq,
            Interval = interval,
            ByDay = byDay,
            Until = until,
            Count = count,
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

    public void Update(
        RecurrenceFrequency freq,
        int interval = 1,
        string? byDay = null,
        DateTimeOffset? until = null,
        int? count = null)
    {
        if (interval < 1)
            throw new ArgumentException("Interval must be at least 1.", nameof(interval));

        Freq = freq;
        Interval = interval;
        ByDay = byDay;
        Until = until;
        Count = count;
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

public enum RecurrenceFrequency
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2
}
