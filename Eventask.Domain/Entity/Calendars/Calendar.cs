using Eventask.Domain.Contracts;
using Eventask.Domain.Entity.Calendars.ScheduleItems;

namespace Eventask.Domain.Entity.Calendars;

public class Calendar : ISynchronizableEntity
{
    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Color { get; private set; }

    public int Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<CalendarMember> Members { get; set; } = [ ];

    public List<ScheduleItem> ScheduleItems { get; set; } = [ ];

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private Calendar ( ) { }

    /// <summary>
    /// Factory method to create a new Calendar with an owner membership.
    /// </summary>
    public static Calendar Create (Guid ownerId, string name, string? color = null)
    {
        if ( string.IsNullOrWhiteSpace(name) )
            throw new ArgumentException("Calendar name cannot be empty.", nameof(name));

        var now = DateTimeOffset.UtcNow;
        var calendar = new Calendar
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = name.Trim(),
            Color = color,
            Version = 0,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        // Seed owner membership
        var ownerMember = CalendarMember.Create(calendar.Id, ownerId, CalendarMemberRole.Owner);
        calendar.Members.Add(ownerMember);

        return calendar;
    }

    /// <summary>
    /// Marks the entity as modified. Updates UpdatedAt only.
    /// Version is incremented server-side by the repository after successful persistence.
    /// </summary>
    private void MarkModified ( )
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }


    #region Permission Checks

    /// <summary>
    /// Returns the role of the given user in this calendar, or null if not a member.
    /// </summary>
    public CalendarMemberRole? GetMemberRole (Guid userId)
    {
        var member = Members.FirstOrDefault(m => m.UserId == userId && !m.IsDeleted);
        return member?.Role;
    }

    /// <summary>
    /// Throws if the user is not at least an Editor (Owner or Editor).
    /// </summary>
    public void RequireEditorOrOwner (Guid userId)
    {
        var role = GetMemberRole(userId);
        if ( role is null )
            throw new UnauthorizedAccessException("User is not a member of this calendar.");
        if ( role == CalendarMemberRole.Viewer )
            throw new UnauthorizedAccessException("User does not have edit permissions on this calendar.");
    }

    /// <summary>
    /// Throws if the user is not the Owner.
    /// </summary>
    public void RequireOwner (Guid userId)
    {
        var role = GetMemberRole(userId);
        if ( role != CalendarMemberRole.Owner )
            throw new UnauthorizedAccessException("Only the owner can perform this action.");
    }

    #endregion

    #region Self 

    public void Rename (string newName, Guid actorUserId)
    {
        RequireEditorOrOwner(actorUserId);
        if ( string.IsNullOrWhiteSpace(newName) )
            throw new ArgumentException("Calendar name cannot be empty.", nameof(newName));
        Name = newName.Trim();
        MarkModified();
    }

    public void ChangeColor (string? newColor, Guid actorUserId)
    {
        RequireEditorOrOwner(actorUserId);
        Color = newColor;
        MarkModified();
    }

    public void SoftDelete (Guid actorUserId)
    {
        RequireOwner(actorUserId);
        if ( IsDeleted )
            return;

        var now = DateTimeOffset.UtcNow;
        IsDeleted = true;
        DeletedAt = now;
        MarkModified();

        // Cascade soft-delete to members and items
        foreach ( var member in Members.Where(m => !m.IsDeleted) )
        {
            member.MarkDeleted(now);
        }
        foreach ( var item in ScheduleItems.Where(i => !i.IsDeleted) )
        {
            item.MarkDeleted(now);
        }
    }

    public void Restore (Guid actorUserId)
    {
        RequireOwner(actorUserId);
        if ( !IsDeleted )
            return;

        IsDeleted = false;
        DeletedAt = null;
        MarkModified();

        // Restore owner membership
        var ownerMember = Members.FirstOrDefault(m => m.UserId == OwnerId);
        ownerMember?.Restore();
    }


    #endregion

    #region Member Related

    public (CalendarMember, bool isExisting) AddMember (Guid userId, CalendarMemberRole role, Guid actorUserId)
    {
        RequireOwner(actorUserId);
        if ( role == CalendarMemberRole.Owner )
            throw new InvalidOperationException("Cannot add another owner; transfer ownership instead.");

        var existing = Members.FirstOrDefault(m => m.UserId == userId);
        if ( existing != null && !existing.IsDeleted )
            throw new InvalidOperationException("User is already a member of this calendar.");

        if ( existing != null && existing.IsDeleted )
        {
            existing.Restore();
            existing.ChangeRole(role);
            MarkModified();
            return (existing, true);
        }
        else
        {
            var member = CalendarMember.Create(Id, userId, role);
            Members.Add(member);
            MarkModified();
            return (member, false);
        }
    }

    public void ChangeMemberRole (Guid userId, CalendarMemberRole newRole, Guid actorUserId)
    {
        RequireOwner(actorUserId);
        if ( userId == OwnerId )
            throw new InvalidOperationException("Cannot change the owner's role.");
        if ( newRole == CalendarMemberRole.Owner )
            throw new InvalidOperationException("Cannot promote to owner; transfer ownership instead.");

        var member = Members.FirstOrDefault(m => m.UserId == userId && !m.IsDeleted)
                     ?? throw new InvalidOperationException("Member not found in the calendar.");
        member.ChangeRole(newRole);
        MarkModified();
    }

    public void RemoveMember (Guid userId, Guid actorUserId)
    {
        RequireOwner(actorUserId);
        if ( userId == OwnerId )
            throw new InvalidOperationException("Cannot remove the owner from the calendar.");

        var member = Members.FirstOrDefault(m => m.UserId == userId && !m.IsDeleted)
                     ?? throw new InvalidOperationException("Member not found in the calendar.");
        member.MarkDeleted(DateTimeOffset.UtcNow);
        MarkModified();
    }

    #endregion

    #region Schedule Item Related

    public ScheduleEvent CreateEvent (
        string title,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        bool allDay,
        Guid actorUserId,
        string? description = null,
        string? location = null)
    {
        RequireEditorOrOwner(actorUserId);
        var item = ScheduleEvent.Create(Id, title, startAt, endAt, allDay, description, location);
        ScheduleItems.Add(item);
        MarkModified();
        return item;
    }

    public ScheduleTask CreateTask (
        string title,
        Guid actorUserId,
        DateTimeOffset? dueAt = null,
        string? description = null,
        string? location = null)
    {
        RequireEditorOrOwner(actorUserId);
        var item = ScheduleTask.Create(Id, title, dueAt, description, location);
        ScheduleItems.Add(item);
        MarkModified();
        return item;
    }

    public ScheduleItem GetScheduleItem (Guid itemId)
    {
        return ScheduleItems.FirstOrDefault(i => i.Id == itemId && !i.IsDeleted)
            ?? throw new InvalidOperationException("Schedule item not found in the calendar.");
    }

    public void DeleteScheduleItem (Guid itemId, Guid actorUserId)
    {
        RequireEditorOrOwner(actorUserId);
        var item = GetScheduleItem(itemId);
        item.MarkDeleted(DateTimeOffset.UtcNow);
        MarkModified();
    }

    public void RestoreScheduleItem (Guid itemId, Guid actorUserId)
    {
        RequireEditorOrOwner(actorUserId);
        var item = ScheduleItems.FirstOrDefault(i => i.Id == itemId && i.IsDeleted)
            ?? throw new InvalidOperationException("Deleted schedule item not found.");
        item.Restore();
        MarkModified();
    }

    #endregion
}

public interface ICalendarRepository
{
    Task<Calendar?> GetAsync (Guid calendarId, CalendarQueryOptions options, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Calendar>> ListByOwnerAsync (Guid ownerId, CalendarQueryOptions? options = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Calendar>> ListByMemberAsync (Guid userId, CalendarQueryOptions? options = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Calendar>> ListChangedSinceAsync (DateTimeOffset changedSince, int pageSize, CalendarQueryOptions? options = null, CancellationToken cancellationToken = default);

    Task<Calendar> AddAsync (Calendar calendar, CancellationToken cancellationToken = default);

    Task UpdateAsync (Calendar calendar, int expectedVersion, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync (Guid calendarId, DateTimeOffset deletedAt, CancellationToken cancellationToken = default);
}

public sealed record CalendarQueryOptions
{
    public bool IncludeMembers { get; init; }

    public bool IncludeScheduleItems { get; init; }
    public bool IncludeAttachments { get; init; }

    public bool IncludeDeleted { get; init; }

    public int? Skip { get; init; }

    public int? Take { get; init; }
}