using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Eventask.ApiService.Repository;
using Eventask.Domain.Dtos;
using Eventask.Domain.Entity.Calendars;
using Eventask.Domain.Entity.Calendars.ScheduleItems;
using Eventask.Domain.Entity.Users;
using Eventask.Domain.Requests;

namespace Eventask.ApiService.Endpoints;

public static partial class ApiEndpoints
{
    extension(RouteGroupBuilder api)
    {
        private RouteGroupBuilder MapCalendars ( )
        {
            var group = api.MapGroup("/calendars").WithTags("Calendars");

            // GET /calendars - List all calendars the user is a member of
            group.MapGet("/", async (
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository) =>
                {
                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    var calendars = await calendarRepository.ListByMemberAsync(
                        userId.Value,
                        new CalendarQueryOptions { IncludeMembers = true });

                    var dtos = calendars.Select(ToCalendarDto).ToList();
                    return Results.Ok(dtos);
                })
                .RequireAuthorization()
                .WithName("Calendars_List")
                .Produces<IReadOnlyList<CalendarDto>>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            // POST /calendars - Create a new calendar
            group.MapPost("/", async (
                    CreateCalendarRequest request,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IUnitOfWork unitOfWork) =>
                {
                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    if ( String.IsNullOrWhiteSpace(request.Name) )
                        return Results.Problem(
                            title: "Invalid request",
                            detail: "Calendar name is required.",
                            statusCode: StatusCodes.Status400BadRequest);

                    try
                    {
                        var calendar = Calendar.Create(userId.Value, request.Name);
                        await calendarRepository.AddAsync(calendar);
                        await unitOfWork.SaveChangesAsync();
                        return Results.Ok(ToCalendarDto(calendar));
                    }
                    catch ( ArgumentException ex )
                    {
                        return Results.Problem(
                            title: "Invalid request",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                })
                .RequireAuthorization()
                .WithName("Calendars_Create")
                .Produces<CalendarDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            // PUT /calendars/{id} - Update a calendar
            group.MapPut("/{id:guid}", async (
                    Guid id,
                    UpdateCalendarRequest request,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IUnitOfWork unitOfWork) =>
                {
                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    var calendar = await calendarRepository.GetAsync(
                        id,
                        new CalendarQueryOptions { IncludeMembers = true });

                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {id} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    var expectedVersion = calendar.Version;

                    try
                    {
                        calendar.Rename(request.Name, userId.Value);
                        await calendarRepository.UpdateAsync(calendar, expectedVersion);
                        await unitOfWork.SaveChangesAsync();
                        return Results.Ok(ToCalendarDto(calendar));
                    }
                    catch ( UnauthorizedAccessException ex )
                    {
                        return Results.Problem(
                            title: "Forbidden",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status403Forbidden);
                    }
                    catch ( ArgumentException ex )
                    {
                        return Results.Problem(
                            title: "Invalid request",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                    catch ( ConcurrencyException ex )
                    {
                        return Results.Problem(
                            title: "Conflict",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status409Conflict,
                            extensions: new Dictionary<string, object?>
                            {
                                ["currentEntity"] = ex.CurrentEntity is Calendar c ? ToCalendarDto(c) : null
                            });
                    }
                })
                .RequireAuthorization()
                .WithName("Calendars_Update")
                .Produces<CalendarDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            // DELETE /calendars/{id} - Soft delete a calendar
            group.MapDelete("/{id:guid}", async (
                    Guid id,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IUnitOfWork unitOfWork) =>
                {
                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    var calendar = await calendarRepository.GetAsync(
                        id,
                        new CalendarQueryOptions { IncludeMembers = true, IncludeScheduleItems = true });

                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {id} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    var expectedVersion = calendar.Version;

                    try
                    {
                        calendar.SoftDelete(userId.Value);
                        await calendarRepository.UpdateAsync(calendar, expectedVersion);
                        await unitOfWork.SaveChangesAsync();
                        return Results.NoContent();
                    }
                    catch ( UnauthorizedAccessException ex )
                    {
                        return Results.Problem(
                            title: "Forbidden",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status403Forbidden);
                    }
                    catch ( ConcurrencyException ex )
                    {
                        return Results.Problem(
                            title: "Conflict",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                })
                .RequireAuthorization()
                .WithName("Calendars_Delete")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            // GET /calendars/{id}/members - List calendar members
            group.MapGet("/{id:guid}/members", async (
                    Guid id,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IUserRepository userRepository) =>
                {
                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    var calendar = await calendarRepository.GetAsync(
                        id,
                        new CalendarQueryOptions { IncludeMembers = true });

                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {id} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    // Check if user is a member
                    if ( calendar.GetMemberRole(userId.Value) is null )
                        return Results.Problem(
                            title: "Forbidden",
                            detail: "You are not a member of this calendar.",
                            statusCode: StatusCodes.Status403Forbidden);

                    var activeMembers = calendar.Members
                        .Where(m => !m.IsDeleted)
                        .ToList();

                    var usernameByUserId = new Dictionary<Guid, string>();

                    foreach ( var member in activeMembers )
                    {
                        if ( usernameByUserId.ContainsKey(member.UserId) )
                            continue;

                        var memberUser = await userRepository.GetByIdAsync(member.UserId);
                        usernameByUserId[member.UserId] = memberUser?.UserName ?? String.Empty;
                    }

                    var members = activeMembers
                        .Select(m => new MemberDto(
                            m.Id,
                            m.UserId,
                            usernameByUserId.GetValueOrDefault(m.UserId, String.Empty),
                            m.Role))
                        .ToList();

                    return Results.Ok(members);
                })
                .RequireAuthorization()
                .WithName("CalendarMembers_List")
                .Produces<List<MemberDto>>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound);

            // POST /calendars/{id}/members - Add a member to calendar
            group.MapPost("/{id:guid}/members", async (
                    Guid id,
                    AddCalendarMemberRequest request,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IUserRepository userRepository,
                    ICalendarMemberRepository memberRepository,
                    IUnitOfWork unitOfWork) =>
                {
                    var actorUserId = GetUserId(user);
                    if ( actorUserId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    var calendar = await calendarRepository.GetAsync(
                        id,
                        new CalendarQueryOptions { IncludeMembers = true });

                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {id} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    // Find target user by username
                    var targetUser = await userRepository.GetByUsernameAsync(request.Username);
                    if ( targetUser is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"User '{request.Username}' not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    var expectedVersion = calendar.Version;

                    try
                    {
                        var (member, isExisting) = calendar.AddMember(targetUser.Id, request.Role, actorUserId.Value);
                        if ( !isExisting )
                            await memberRepository.AddTrackingAsync(member);
                        await calendarRepository.UpdateAsync(calendar, expectedVersion);
                        await unitOfWork.SaveChangesAsync();
                        return Results.NoContent();
                    }
                    catch ( UnauthorizedAccessException ex )
                    {
                        return Results.Problem(
                            title: "Forbidden",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status403Forbidden);
                    }
                    catch ( InvalidOperationException ex )
                    {
                        return Results.Problem(
                            title: "Invalid request",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                    catch ( ConcurrencyException ex )
                    {
                        return Results.Problem(
                            title: "Conflict",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                })
                .RequireAuthorization()
                .WithName("CalendarMembers_Add")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            // DELETE /calendars/{id}/members/{memberId} - Remove a member from calendar
            group.MapDelete("/{id:guid}/members/{memberId:guid}", async (
                    Guid id,
                    Guid memberId,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IUnitOfWork unitOfWork) =>
                {
                    var actorUserId = GetUserId(user);
                    if ( actorUserId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    var calendar = await calendarRepository.GetAsync(
                        id,
                        new CalendarQueryOptions { IncludeMembers = true });

                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {id} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    // Find the member to remove by their user ID
                    var member = calendar.Members.FirstOrDefault(m => m.UserId == memberId && !m.IsDeleted);
                    if ( member is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Member with user ID {memberId} not found in this calendar.",
                            statusCode: StatusCodes.Status404NotFound);

                    var expectedVersion = calendar.Version;

                    try
                    {
                        calendar.RemoveMember(memberId, actorUserId.Value);
                        await calendarRepository.UpdateAsync(calendar, expectedVersion);
                        await unitOfWork.SaveChangesAsync();
                        return Results.NoContent();
                    }
                    catch ( UnauthorizedAccessException ex )
                    {
                        return Results.Problem(
                            title: "Forbidden",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status403Forbidden);
                    }
                    catch ( InvalidOperationException ex )
                    {
                        return Results.Problem(
                            title: "Invalid request",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                    catch ( ConcurrencyException ex )
                    {
                        return Results.Problem(
                            title: "Conflict",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                })
                .RequireAuthorization()
                .WithName("CalendarMembers_Remove")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            // GET /calendars/{id}/items - List schedule items in a calendar
            group.MapGet("/{id:guid}/items", async (
                    Guid id,
                    DateTimeOffset? from,
                    DateTimeOffset? to,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository) =>
                {
                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    var calendar = await calendarRepository.GetAsync(
                        id,
                        new CalendarQueryOptions { IncludeMembers = true, IncludeScheduleItems = true });

                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {id} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    // Check if user is a member
                    if ( calendar.GetMemberRole(userId.Value) is null )
                        return Results.Problem(
                            title: "Forbidden",
                            detail: "You are not a member of this calendar.",
                            statusCode: StatusCodes.Status403Forbidden);

                    var items = calendar.ScheduleItems.Where(i => !i.IsDeleted);

                    // Optional date filtering for events
                    if ( from.HasValue || to.HasValue )
                        items = items.Where(i =>
                        {
                            if ( i is ScheduleEvent evt )
                            {
                                var afterFrom = !from.HasValue || evt.EndAt >= from.Value;
                                var beforeTo = !to.HasValue || evt.StartAt <= to.Value;
                                return afterFrom && beforeTo;
                            }

                            if ( i is ScheduleTask task && task.DueAt.HasValue )
                            {
                                var afterFrom = !from.HasValue || task.DueAt >= from.Value;
                                var beforeTo = !to.HasValue || task.DueAt <= to.Value;
                                return afterFrom && beforeTo;
                            }

                            return true; // Tasks without due date are always included
                        });

                    var dtos = items.Select(ToScheduleItemDto).ToList();
                    return Results.Ok(dtos);
                })
                .RequireAuthorization()
                .WithName("ScheduleItems_ListByCalendar")
                .Produces<IReadOnlyList<ScheduleItemDto>>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound);

            // POST /calendars/{id}/items - Create a schedule item
            group.MapPost("/{id:guid}/items", async (
                    Guid id,
                    CreateScheduleItemRequest request,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IScheduleItemRepository scheduleItemRepository,
                    IUnitOfWork unitOfWork) =>
                {
                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    var calendar = await calendarRepository.GetAsync(
                        id,
                        new CalendarQueryOptions { IncludeMembers = true, IncludeScheduleItems = false });

                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {id} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    var expectedVersion = calendar.Version;

                    try
                    {
                        ScheduleItem item;
                        if ( request.Type.Equals("Event", StringComparison.OrdinalIgnoreCase) )
                        {
                            if ( !request.StartAt.HasValue || !request.EndAt.HasValue )
                                return Results.Problem(
                                    title: "Invalid request",
                                    detail: "StartAt and EndAt are required for events.",
                                    statusCode: StatusCodes.Status400BadRequest);

                            item = calendar.CreateEvent(
                                request.Title,
                                request.StartAt.Value,
                                request.EndAt.Value,
                                request.AllDay,
                                userId.Value,
                                request.Description,
                                request.Location);
                        }
                        else if ( request.Type.Equals("Task", StringComparison.OrdinalIgnoreCase) )
                        {
                            item = calendar.CreateTask(
                                request.Title,
                                userId.Value,
                                request.DueAt,
                                request.Description,
                                request.Location);
                        }
                        else
                        {
                            return Results.Problem(
                                title: "Invalid request",
                                detail: $"Invalid type '{request.Type}'. Valid types: Event, Task.",
                                statusCode: StatusCodes.Status400BadRequest);
                        }

                        // Weird workaround as EF Core can't correctly label new item
                        // added by HasMany navigations as Created. 
                        // OwnsMany seems work, but it doesn't support TPH/Discriminator. :(
                        // OwnsMany with Discriminator didn't make it come at EF Core 10, 
                        // and this workaround might not be needed in the future EF releases.
                        // Now we have to manually add it to DbSet, then it will be recognized.
                        await scheduleItemRepository.AddNewItemTrackingAsync(item);
                        await calendarRepository.UpdateAsync(calendar, expectedVersion);
                        await unitOfWork.SaveChangesAsync();
                        return Results.Ok(ToScheduleItemDto(item));
                    }
                    catch ( UnauthorizedAccessException ex )
                    {
                        return Results.Problem(
                            title: "Forbidden",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status403Forbidden);
                    }
                    catch ( ArgumentException ex )
                    {
                        return Results.Problem(
                            title: "Invalid request",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                    catch ( ConcurrencyException ex )
                    {
                        return Results.Problem(
                            title: "Conflict",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                })
                .RequireAuthorization()
                .WithName("ScheduleItems_Create")
                .Produces<ScheduleItemDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            return api;
        }
    }

    // Helper Methods

    private static Guid? GetUserId (ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static CalendarDto ToCalendarDto (Calendar calendar)
    {
        return new CalendarDto(
            calendar.Id,
            calendar.Name,
            calendar.Version,
            calendar.UpdatedAt,
            calendar.IsDeleted);
    }

    private static ScheduleItemDto ToScheduleItemDto (ScheduleItem item)
    {
        return item switch
        {
            ScheduleEvent evt => new ScheduleItemDto(
                evt.Id,
                evt.CalendarId,
                "Event",
                evt.Title,
                evt.Description,
                evt.Location,
                evt.StartAt,
                evt.EndAt,
                null,
                evt.AllDay,
                false,
                null,
                evt.Version,
                evt.UpdatedAt,
                evt.IsDeleted),
            ScheduleTask task => new ScheduleItemDto(
                task.Id,
                task.CalendarId,
                "Task",
                task.Title,
                task.Description,
                task.Location,
                null,
                null,
                task.DueAt,
                false,
                task.IsCompleted,
                task.CompletedAt,
                task.Version,
                task.UpdatedAt,
                task.IsDeleted),
            _ => throw new InvalidOperationException($"Unknown schedule item type: {item.GetType().Name}")
        };
    }
}