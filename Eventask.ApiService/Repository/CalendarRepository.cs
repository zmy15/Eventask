using Eventask.Domain.Entity.Calendars;
using Microsoft.EntityFrameworkCore;

namespace Eventask.ApiService.Repository;

/// <summary>
/// Thrown when an optimistic concurrency conflict is detected.
/// </summary>
public class ConcurrencyException (string message, object? currentEntity = null) : Exception(message)
{
    public object? CurrentEntity { get; } = currentEntity;
}

public class CalendarRepository (EventaskContext db) : ICalendarRepository
{
    public async Task<Calendar?> GetAsync (Guid calendarId, CalendarQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        var query = db.Calendars.AsQueryable();

        if ( !options.IncludeDeleted )
            query = query.Where(c => !c.IsDeleted);

        if ( options.IncludeMembers )
            query = query.Include(c => c.Members);

        if ( options.IncludeScheduleItems )
            query = query.Include(c => c.ScheduleItems).ThenInclude(c => c.Attachments);


        return await query.FirstOrDefaultAsync(c => c.Id == calendarId, cancellationToken);
    }

    public async Task<IReadOnlyList<Calendar>> ListByOwnerAsync (Guid ownerId, CalendarQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CalendarQueryOptions();

        var query = db.Calendars.AsQueryable();

        if ( !options.IncludeDeleted )
            query = query.Where(c => !c.IsDeleted);

        query = query.Where(c => c.OwnerId == ownerId);

        if ( options.IncludeMembers )
            query = query.Include(c => c.Members);

        if ( options.IncludeScheduleItems )
            query = query.Include(c => c.ScheduleItems);

        if ( options.Skip.HasValue )
            query = query.Skip(options.Skip.Value);

        if ( options.Take.HasValue )
            query = query.Take(options.Take.Value);

        var calendars = await query.ToListAsync(cancellationToken);
        return calendars.AsReadOnly();
    }

    public async Task<IReadOnlyList<Calendar>> ListByMemberAsync (Guid userId, CalendarQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CalendarQueryOptions();

        var query = db.Calendars.AsQueryable();

        if ( !options.IncludeDeleted )
            query = query.Where(c => !c.IsDeleted);

        // Filter by membership
        query = query.Where(c => c.Members.Any(m => m.UserId == userId && !m.IsDeleted));

        if ( options.IncludeMembers )
            query = query.Include(c => c.Members);

        if ( options.IncludeScheduleItems )
            query = query.Include(c => c.ScheduleItems).ThenInclude(c => c.Attachments);

        if ( options.Skip.HasValue )
            query = query.Skip(options.Skip.Value);

        if ( options.Take.HasValue )
            query = query.Take(options.Take.Value);

        var calendars = await query.ToListAsync(cancellationToken);
        return calendars.AsReadOnly();
    }

    public async Task<IReadOnlyList<Calendar>> ListChangedSinceAsync (DateTimeOffset changedSince, int pageSize,
        CalendarQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CalendarQueryOptions();

        var query = db.Calendars.AsQueryable();

        query = query.Where(c => c.UpdatedAt > changedSince);

        if ( options.IncludeMembers )
            query = query.Include(c => c.Members);

        if ( options.IncludeScheduleItems )
            query = query.Include(c => c.ScheduleItems);

        var calendars = await query
            .OrderBy(c => c.UpdatedAt)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return calendars.AsReadOnly();
    }

    /// <summary>
    /// Adds a calendar to the tracking context. Changes will be persisted when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    public Task<Calendar> AddAsync (Calendar calendar, CancellationToken cancellationToken = default)
    {
        db.Calendars.Add(calendar);
        return Task.FromResult(calendar);
    }

    /// <summary>
    /// Prepares a calendar for update by incrementing its version. Changes will be persisted when UnitOfWork.SaveChangesAsync is called.
    /// Note: Version is incremented here for optimistic concurrency; EF Core will detect conflicts during save.
    /// </summary>
    public Task UpdateAsync (Calendar calendar, int expectedVersion, CancellationToken cancellationToken = default)
    {
        calendar.Version = expectedVersion + 1;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Soft deletes a calendar. Changes will be persisted when UnitOfWork.SaveChangesAsync is called.
    /// </summary>
    public async Task SoftDeleteAsync (Guid calendarId, DateTimeOffset deletedAt,
        CancellationToken cancellationToken = default)
    {
        var calendar = await db.Calendars.FirstOrDefaultAsync(c => c.Id == calendarId, cancellationToken);

        if ( calendar == null )
            throw new InvalidOperationException($"Calendar with ID {calendarId} not found.");

        calendar.IsDeleted = true;
        calendar.DeletedAt = deletedAt;
        calendar.Version++;
        calendar.UpdatedAt = deletedAt;
    }
}