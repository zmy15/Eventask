using System.Security.Claims;
using Eventask.ApiService.Services.Storage;
using Eventask.Domain.Entity.Calendars;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Eventask.ApiService.Endpoints;

public static partial class ApiEndpoints
{
    extension(RouteGroupBuilder api)
    {
        private RouteGroupBuilder MapAttachments()
        {
            var group = api.MapGroup("/attachments").WithTags("Attachments");

            group.MapGet("/{id:guid}/download", async (
                    Guid id,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IObjectStorageService storageService) =>
                {
                    var userId = GetUserId(user);
                    if (userId is null)
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    var calendars = await calendarRepository.ListByMemberAsync(
                        userId.Value,
                        new CalendarQueryOptions { IncludeMembers = true, IncludeScheduleItems = true });

                    var exp = calendars
                        .SelectMany(c => c.ScheduleItems)
                        .SelectMany(i => i.Attachments).ToList();
                    var attachment = exp
                        .FirstOrDefault(a => a.Id == id && !a.IsDeleted);

                    if (attachment is null)
                        return Results.Problem(
                            title: "Not found",
                            detail: $"Attachment with ID {id} not found.",
                            statusCode: StatusCodes.Status404NotFound);

                    // Ensure caller is member of the calendar owning the item
                    var calendar = calendars.First(c => c.ScheduleItems.Any(i => i.Attachments.Any(a => a.Id == id)));
                    if (calendar.GetMemberRole(userId.Value) is null)
                        return Results.Problem(
                            title: "Forbidden",
                            detail: "You are not a member of this calendar.",
                            statusCode: StatusCodes.Status403Forbidden);

                    try
                    {
                        var (stream, contentType, size) = await storageService.DownloadAsync(attachment.ObjectKey);
                        return Results.File(stream, contentType, attachment.FileName, enableRangeProcessing: true,
                            lastModified: attachment.UpdatedAt, entityTag: null);
                    }
                    catch (Exception ex)
                    {
                        return Results.Problem(
                            title: "Download failed",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status500InternalServerError);
                    }
                })
                .RequireAuthorization()
                .WithName("Attachments_Download")
                .Produces<FileStreamHttpResult>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError);

            return api;
        }
    }
}