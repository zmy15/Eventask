using Eventask.Domain.Contracts;

namespace Eventask.Domain.Entity.Calendars;

public class CalendarMember : ISynchronizableEntity
{
    public Guid Id { get; private set; }

    public Guid CalendarId { get; private set; }

    public Guid UserId { get; private set; }

    public CalendarMemberRole Role { get; private set; }

    public int Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Calendar? Calendar { get; set; }

    /// Private constructor for EF Core
    private CalendarMember ( ) { }

    /// <summary>
    /// Factory method to create a new CalendarMember.
    /// </summary>
    public static CalendarMember Create (Guid calendarId, Guid userId, CalendarMemberRole role)
    {
        var now = DateTimeOffset.UtcNow;
        return new CalendarMember
        {
            Id = Guid.NewGuid(),
            CalendarId = calendarId,
            UserId = userId,
            Role = role,
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
    private void MarkModified ( )
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }


    public void ChangeRole (CalendarMemberRole newRole)
    {
        if ( Role == CalendarMemberRole.Owner )
            throw new InvalidOperationException("Cannot change the owner's role directly.");
        if ( newRole == CalendarMemberRole.Owner )
            throw new InvalidOperationException("Cannot promote to owner; use ownership transfer.");
        Role = newRole;
        MarkModified();
    }

    public void MarkDeleted (DateTimeOffset deletedAt)
    {
        if ( IsDeleted )
            return;
        IsDeleted = true;
        DeletedAt = deletedAt;
        MarkModified();
    }

    public void Restore ( )
    {
        if ( !IsDeleted )
            return;
        IsDeleted = false;
        DeletedAt = null;
        MarkModified();
    }
}

public enum CalendarMemberRole
{
    Owner = 0,
    Editor = 1,
    Viewer = 2
}

public interface ICalendarMemberRepository
{
    Task AddTrackingAsync (CalendarMember member);
}