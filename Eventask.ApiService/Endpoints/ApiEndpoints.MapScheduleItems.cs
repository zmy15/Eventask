using System.Security.Claims;
using Eventask.ApiService.Repository;
using Eventask.ApiService.Services.Storage;
using Eventask.Domain.Dtos;
using Eventask.Domain.Entity.Calendars;
using Eventask.Domain.Entity.Calendars.ScheduleItems;
using Eventask.Domain.Requests;

namespace Eventask.ApiService.Endpoints;

public static partial class ApiEndpoints
{
    extension(RouteGroupBuilder api)
    {
        private RouteGroupBuilder MapScheduleItems ( )
        {
            var group = api.MapGroup("/calendars").WithTags("ScheduleItems");

            // PUT /items/{id} - Update a schedule item
            group.MapPut("/{calendarId:guid}/items/{id:guid}", async (
                    Guid calendarId,
                    Guid id,
                    UpdateScheduleItemRequest request,
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

                    // Find the calendar containing this item
                    var calendar = await calendarRepository.GetAsync(calendarId,
                        new CalendarQueryOptions { IncludeMembers = true, IncludeScheduleItems = true });
                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {calendarId} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    if ( calendar.Members.All(x => x.UserId != userId.Value) )
                        return Results.Problem(
                            title: "Forbidden",
                            detail: "You are not a member of this calendar.",
                            statusCode: StatusCodes.Status403Forbidden);

                    var item = calendar.GetScheduleItem(id);
                    var expectedVersion = calendar.Version;

                    try
                    {
                        // Update common fields
                        item.UpdateDetails(request.Title, request.Description, request.Location);

                        // Update type-specific fields
                        if ( item is ScheduleEvent evt )
                        {
                            if ( !request.StartAt.HasValue || !request.EndAt.HasValue )
                                return Results.Problem(
                                    title: "Invalid request",
                                    detail: "StartAt and EndAt are required for events.",
                                    statusCode: StatusCodes.Status400BadRequest);

                            evt.Reschedule(request.StartAt.Value, request.EndAt.Value, request.AllDay);
                        }
                        else if ( item is ScheduleTask task )
                        {
                            task.SetDueAt(request.DueAt);
                            if ( request.IsCompleted && !task.IsCompleted )
                                task.MarkComplete();
                            else if ( !request.IsCompleted && task.IsCompleted )
                                task.Reopen();
                        }

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
                            statusCode: StatusCodes.Status409Conflict,
                            extensions: new Dictionary<string, object?>
                            {
                                ["currentEntity"] = ex.CurrentEntity is Calendar c
                                    ? c.ScheduleItems.FirstOrDefault(i => i.Id == id) is { } currentItem
                                        ? ToScheduleItemDto(currentItem)
                                        : null
                                    : null
                            });
                    }
                })
                .RequireAuthorization()
                .WithName("ScheduleItems_Update")
                .Produces<ScheduleItemDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            // DELETE /items/{id} - Soft delete a schedule item
            group.MapDelete("/{calendarId:guid}/items/{id:guid}", async (
                    Guid calendarId,
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

                    // Find the calendar containing this item
                    var calendar = await calendarRepository.GetAsync(calendarId,
                        new CalendarQueryOptions { IncludeMembers = true, IncludeScheduleItems = true });
                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {calendarId} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    if ( calendar.Members.All(x => x.UserId != userId.Value) )
                        return Results.Problem(
                            title: "Forbidden",
                            detail: "You are not a member of this calendar.",
                            statusCode: StatusCodes.Status403Forbidden);

                    var expectedVersion = calendar.Version;

                    try
                    {
                        calendar.DeleteScheduleItem(id, userId.Value);
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
                            title: "Not found",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status404NotFound);
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
                .WithName("ScheduleItems_Delete")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            // POST /items/{id}/attachments - Upload attachment (placeholder)
            group.MapPost("/{calendarId:guid}/items/{id:guid}/attachments", async (
                    Guid calendarId,
                    Guid id,
                    IFormFile file,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IObjectStorageService storageService,
                    IAttachmentRepository attachmentRepository,
                    IUnitOfWork unitOfWork) =>
                {
                    if ( file is null || file.Length == 0 )
                        return Results.Problem(
                            title: "Invalid file",
                            detail: "File is missing or empty.",
                            statusCode: StatusCodes.Status400BadRequest);

                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    // Load calendar and item
                    var calendar = await calendarRepository.GetAsync(calendarId,
                        new CalendarQueryOptions { IncludeMembers = true, IncludeScheduleItems = true });
                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {calendarId} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    if ( calendar.Members.All(x => x.UserId != userId.Value) )
                        return Results.Problem(
                            title: "Forbidden",
                            detail: "You are not a member of this calendar.",
                            statusCode: StatusCodes.Status403Forbidden);

                    var item = calendar.ScheduleItems.FirstOrDefault(i => i.Id == id && !i.IsDeleted);
                    if ( item is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Schedule item with ID {id} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    var expectedVersion = calendar.Version;

                    // Generate object key: attachments/{scheduleItemId}/{guid-filename}
                    var objectKey = $"attachments/{id}/{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

                    try
                    {
                        // Start transaction to ensure atomicity
                        await unitOfWork.BeginTransactionAsync();

                        // Upload to object storage
                        await storageService.UploadAsync(objectKey, file);

                        // Persist attachment metadata
                        var attachment = item.AddAttachment(
                            file.FileName,
                            file.ContentType ?? "application/octet-stream",
                            file.Length,
                            objectKey);

                        await attachmentRepository.AddNewItemTrackingAsync(attachment);
                        await calendarRepository.UpdateAsync(calendar, expectedVersion);

                        await unitOfWork.CommitAsync();

                        return Results.Ok(new AttachmentDto(
                            attachment.Id,
                            attachment.ScheduleItemId,
                            attachment.FileName,
                            attachment.ContentType,
                            attachment.Size,
                            attachment.Version,
                            attachment.UpdatedAt,
                            attachment.IsDeleted));
                    }
                    catch ( UnauthorizedAccessException ex )
                    {
                        await unitOfWork.RollbackAsync();
                        return Results.Problem(
                            title: "Forbidden",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status403Forbidden);
                    }
                    catch ( ConcurrencyException ex )
                    {
                        await unitOfWork.RollbackAsync();
                        return Results.Problem(
                            title: "Conflict",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status409Conflict);
                    }
                    catch ( Exception ex )
                    {
                        await unitOfWork.RollbackAsync();
                        return Results.Problem(
                            title: "Upload failed",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status500InternalServerError);
                    }
                })
                .RequireAuthorization()
                .DisableAntiforgery()
                .WithName("Attachments_Upload")
                .Accepts<IFormFile>("multipart/form-data")
                .Produces<AttachmentDto>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status500InternalServerError);

            group.MapGet("/{calendarId:guid}/items/{id:guid}/attachments", async (
                    Guid calendarId,
                    Guid id,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IAttachmentRepository attachmentRepository) =>
                {
                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    // Load calendar and item
                    var calendar = await calendarRepository.GetAsync(calendarId,
                        new CalendarQueryOptions { IncludeMembers = true, IncludeScheduleItems = true });
                    if ( calendar is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Calendar with ID {calendarId} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    if ( calendar.Members.All(x => x.UserId != userId.Value) )
                        return Results.Problem(
                            title: "Forbidden",
                            detail: "You are not a member of this calendar.",
                            statusCode: StatusCodes.Status403Forbidden);

                    var item = calendar.ScheduleItems.FirstOrDefault(i => i.Id == id && !i.IsDeleted);
                    if ( item is null )
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Schedule item with ID {id} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    var attachments = item.Attachments
                        .Where(a => !a.IsDeleted)
                        .Select(attachment => new AttachmentDto(
                            attachment.Id,
                            attachment.ScheduleItemId,
                            attachment.FileName,
                            attachment.ContentType,
                            attachment.Size,
                            attachment.Version,
                            attachment.UpdatedAt,
                            attachment.IsDeleted));

                    return Results.Ok(attachments);
                })
                .RequireAuthorization()
                .DisableAntiforgery()
                .WithName("Attachments_List")
                .Produces<List<AttachmentDto>>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError);

            return api;
        }
    }
}