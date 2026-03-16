using Eventask.Domain.Contracts;

namespace Eventask.Domain.Entity.Calendars.ScheduleItems;

public class ScheduleItem : ISynchronizableEntity
{
    public Guid Id { get; protected set; }

    public Guid CalendarId { get; protected set; }

    public Calendar? Calendar { get; set; }

    public string Title { get; protected set; } = string.Empty;

    public string? Description { get; protected set; }

    public string? Location { get; protected set; }

    public int Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<Reminder> Reminders { get; set; } = [];

    public List<Attachment> Attachments { get; set; } = [];

    // ─── Mutation Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Marks the entity as modified. Updates UpdatedAt only.
    /// Version is incremented server-side by the repository after successful persistence.
    /// </summary>
    protected void MarkModified()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // ─── Common Mutations ───────────────────────────────────────────────────────

    public void UpdateDetails(string title, string? description, string? location)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        Title = title.Trim();
        Description = description;
        Location = location;
        MarkModified();
    }

    public void MarkDeleted(DateTimeOffset deletedAt)
    {
        if (IsDeleted)
            return;
        IsDeleted = true;
        DeletedAt = deletedAt;
        MarkModified();

        // Cascade to reminders and attachments
        foreach (var reminder in Reminders.Where(r => !r.IsDeleted))
        {
            reminder.MarkDeleted(deletedAt);
        }
        foreach (var attachment in Attachments.Where(a => !a.IsDeleted))
        {
            attachment.MarkDeleted(deletedAt);
        }
    }

    public void Restore()
    {
        if (!IsDeleted)
            return;
        IsDeleted = false;
        DeletedAt = null;
        MarkModified();
    }

    // ─── Reminder Management ────────────────────────────────────────────────────

    public Reminder AddReminder(int offsetMinutes)
    {
        var reminder = Reminder.Create(Id, offsetMinutes);
        Reminders.Add(reminder);
        MarkModified();
        return reminder;
    }

    public void RemoveReminder(Guid reminderId)
    {
        var reminder = Reminders.FirstOrDefault(r => r.Id == reminderId && !r.IsDeleted)
            ?? throw new InvalidOperationException("Reminder not found.");
        reminder.MarkDeleted(DateTimeOffset.UtcNow);
        MarkModified();
    }

    // ─── Attachment Management ──────────────────────────────────────────────────

    public Attachment AddAttachment(string fileName, string contentType, long size, string objectKey, string? sha256 = null)
    {
        var attachment = Attachment.Create(Id, fileName, contentType, size, objectKey, sha256);
        Attachments.Add(attachment);
        MarkModified();
        return attachment;
    }

    public void RemoveAttachment(Guid attachmentId)
    {
        var attachment = Attachments.FirstOrDefault(a => a.Id == attachmentId && !a.IsDeleted)
            ?? throw new InvalidOperationException("Attachment not found.");
        attachment.MarkDeleted(DateTimeOffset.UtcNow);
        MarkModified();
    }
}

public class ScheduleEvent : ScheduleItem
{
    public RecurrenceRule? RecurrenceRule { get; private set; }

    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public bool AllDay { get; private set; }

    // Private constructor for EF Core
    private ScheduleEvent() { }

    /// <summary>
    /// Factory method to create a new ScheduleEvent.
    /// </summary>
    public static ScheduleEvent Create(
        Guid calendarId,
        string title,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        bool allDay,
        string? description = null,
        string? location = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (endAt <= startAt)
            throw new ArgumentException("EndAt must be after StartAt.", nameof(endAt));

        var now = DateTimeOffset.UtcNow;
        return new ScheduleEvent
        {
            Id = Guid.NewGuid(),
            CalendarId = calendarId,
            Title = title.Trim(),
            Description = description,
            Location = location,
            StartAt = startAt,
            EndAt = endAt,
            AllDay = allDay,
            Version = 0,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    public void Reschedule(DateTimeOffset newStartAt, DateTimeOffset newEndAt, bool newAllDay)
    {
        if (newEndAt <= newStartAt)
            throw new ArgumentException("EndAt must be after StartAt.", nameof(newEndAt));
        StartAt = newStartAt;
        EndAt = newEndAt;
        AllDay = newAllDay;
        MarkModified();
    }

    public void SetRecurrenceRule(RecurrenceFrequency freq, int interval = 1, string? byDay = null, DateTimeOffset? until = null, int? count = null)
    {
        if (interval < 1)
            throw new ArgumentException("Interval must be at least 1.", nameof(interval));

        if (RecurrenceRule == null)
        {
            RecurrenceRule = RecurrenceRule.Create(Id, freq, interval, byDay, until, count);
        }
        else
        {
            RecurrenceRule.Update(freq, interval, byDay, until, count);
        }
        MarkModified();
    }

    public void ClearRecurrenceRule()
    {
        if (RecurrenceRule != null)
        {
            RecurrenceRule.MarkDeleted(DateTimeOffset.UtcNow);
            MarkModified();
        }
    }
}

public class ScheduleTask : ScheduleItem
{
    public bool IsCompleted { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private ScheduleTask() { }

    /// <summary>
    /// Factory method to create a new ScheduleTask.
    /// </summary>
    public static ScheduleTask Create(
        Guid calendarId,
        string title,
        DateTimeOffset? dueAt = null,
        string? description = null,
        string? location = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        var now = DateTimeOffset.UtcNow;
        return new ScheduleTask
        {
            Id = Guid.NewGuid(),
            CalendarId = calendarId,
            Title = title.Trim(),
            Description = description,
            Location = location,
            DueAt = dueAt,
            IsCompleted = false,
            CompletedAt = null,
            Version = 0,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    public void SetDueAt(DateTimeOffset? newDueAt)
    {
        DueAt = newDueAt;
        MarkModified();
    }

    public void MarkComplete()
    {
        if (IsCompleted)
            return;
        IsCompleted = true;
        CompletedAt = DateTimeOffset.UtcNow;
        MarkModified();
    }

    public void Reopen()
    {
        if (!IsCompleted)
            return;
        IsCompleted = false;
        CompletedAt = null;
        MarkModified();
    }
}

public interface IScheduleItemRepository
{
    /// <summary>
    /// Only used for add tracking!
    /// It is not intended for adding to database directly, but a workaround for EF's limitation.
    /// </summary>
    Task AddNewItemTrackingAsync(ScheduleItem item);
}
