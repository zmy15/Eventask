using System.Security.Claims;
using Eventask.ApiService.Repository;
using Eventask.Domain.Dtos;
using Eventask.Domain.Entity.Calendars;
using Eventask.Domain.Entity.Calendars.ScheduleItems;
using Eventask.Domain.Requests;

namespace Eventask.ApiService.Endpoints;

public static partial class ApiEndpoints
{
    extension(RouteGroupBuilder api)
    {
        private RouteGroupBuilder MapSync ( )
        {
            var group = api.MapGroup("/sync").WithTags("Sync");

            // POST /sync/pull - Pull changed entities since last sync
            group.MapPost("/pull", async (
                    SyncPullRequest request,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IScheduleItemRepository scheduleItemRepository) =>
                {
                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    // Get all calendar IDs the user is a member of (including deleted ones for tombstone sync)
                    var memberCalendars = await calendarRepository.ListByMemberAsync(
                        userId.Value,
                        new CalendarQueryOptions { IncludeDeleted = true });

                    var accessibleCalendarIds = memberCalendars
                        .Select(c => c.Id)
                        .ToList();

                    // Fetch changed calendars (including deleted ones)
                    var changedCalendars = await calendarRepository.ListChangedSinceAsync(
                        request.LastSyncAt,
                        pageSize: 500,
                        new CalendarQueryOptions { IncludeDeleted = true });

                    // Filter to only calendars the user has access to
                    var userChangedCalendars = changedCalendars
                        .Where(c => accessibleCalendarIds.Contains(c.Id))
                        .ToList();

                    // Fetch changed schedule items for accessible calendars
                    var changedItems = accessibleCalendarIds.Count > 0
                        ? await scheduleItemRepository.ListChangedSinceAsync(
                            accessibleCalendarIds,
                            request.LastSyncAt,
                            pageSize: 500)
                        : Array.Empty<ScheduleItem>();

                    // Extract attachments from changed items (including deleted ones for tombstone sync)
                    var changedAttachments = changedItems
                        .SelectMany(i => i.Attachments)
                        .Where(a => a.UpdatedAt > request.LastSyncAt)
                        .DistinctBy(a => a.Id)
                        .ToList();

                    // Map entities to DTOs
                    var calendarDtos = userChangedCalendars.Select(ToCalendarDto).ToList();
                    var itemDtos = changedItems.Select(ToScheduleItemDto).ToList();
                    var attachmentDtos = changedAttachments.Select(a => new AttachmentDto(
                        a.Id,
                        a.ScheduleItemId,
                        a.FileName,
                        a.ContentType,
                        a.Size,
                        a.Version,
                        a.UpdatedAt,
                        a.IsDeleted)).ToList();

                    return Results.Ok(new SyncPullResponse(
                        DateTimeOffset.UtcNow,
                        new SyncDelta(calendarDtos, itemDtos, attachmentDtos)));
                })
                .RequireAuthorization()
                .WithName("Sync_Pull")
                .Produces<SyncPullResponse>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            // POST /sync/push - Push local changes to server
            group.MapPost("/push", async (
                    SyncPushRequest request,
                    ClaimsPrincipal user,
                    ICalendarRepository calendarRepository,
                    IScheduleItemRepository scheduleItemRepository,
                    IAttachmentRepository attachmentRepository,
                    IUnitOfWork unitOfWork) =>
                {
                    var userId = GetUserId(user);
                    if ( userId is null )
                        return Results.Problem(
                            title: "Unauthorized",
                            detail: "User ID not found in token.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    var applied = new List<SyncAppliedChange>();
                    var conflicts = new List<SyncConflict>();

                    try
                    {
                        await unitOfWork.BeginTransactionAsync();

                        // Process calendar changes
                        foreach (var change in request.Calendars)
                        {
                            await ProcessCalendarChangeAsync(
                                change, userId.Value, calendarRepository, applied, conflicts);
                        }

                        // Process schedule item changes
                        foreach (var change in request.Items)
                        {
                            await ProcessScheduleItemChangeAsync(
                                change, userId.Value, calendarRepository, scheduleItemRepository, applied, conflicts);
                        }

                        // Process attachment changes
                        foreach (var change in request.Attachments)
                        {
                            await ProcessAttachmentChangeAsync(
                                change, userId.Value, calendarRepository, attachmentRepository, applied, conflicts);
                        }

                        // If there are conflicts, rollback and return conflict response
                        if (conflicts.Count > 0)
                        {
                            await unitOfWork.RollbackAsync();
                            return Results.Ok(new SyncPushResponse(
                                DateTimeOffset.UtcNow,
                                new SyncPushResult(applied.AsReadOnly(), conflicts.AsReadOnly())));
                        }

                        await unitOfWork.SaveChangesAsync();
                        await unitOfWork.CommitAsync();

                        return Results.Ok(new SyncPushResponse(
                            DateTimeOffset.UtcNow,
                            new SyncPushResult(applied.AsReadOnly(), conflicts.AsReadOnly())));
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        await unitOfWork.RollbackAsync();
                        return Results.Problem(
                            title: "Forbidden",
                            detail: ex.Message,
                            statusCode: StatusCodes.Status403Forbidden);
                    }
                    catch (Exception)
                    {
                        await unitOfWork.RollbackAsync();
                        throw;
                    }
                })
                .RequireAuthorization()
                .WithName("Sync_Push")
                .Produces<SyncPushResponse>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden);

            return group;
        }

        // ─── Push Helpers ──────────────────────────────────────────────────────────

        private static async Task ProcessCalendarChangeAsync(
            SyncCalendarChange change,
            Guid userId,
            ICalendarRepository calendarRepository,
            List<SyncAppliedChange> applied,
            List<SyncConflict> conflicts)
        {
            var dto = change.Entity;

            if (change.Operation == SyncOperation.Upsert)
            {
                var calendar = await calendarRepository.GetAsync(
                    dto.Id,
                    new CalendarQueryOptions { IncludeDeleted = true, IncludeMembers = true });

                if (calendar is null)
                {
                    // Create new calendar
                    calendar = Calendar.Create(userId, dto.Name);
                    await calendarRepository.AddAsync(calendar);
                    applied.Add(new SyncAppliedChange(
                        "Calendar", calendar.Id, calendar.Version, calendar.UpdatedAt));
                    return;
                }

                // Calendar exists — check version conflict
                if (change.ExpectedVersion.HasValue && calendar.Version != change.ExpectedVersion.Value)
                {
                    conflicts.Add(new SyncConflict(
                        "Calendar", calendar.Id, calendar.Version, calendar.UpdatedAt));
                    return;
                }

                // Restore if deleted
                if (calendar.IsDeleted)
                {
                    calendar.Restore(userId);
                }

                // Update calendar name (only mutable field in sync)
                if (!string.IsNullOrWhiteSpace(dto.Name) && calendar.Name != dto.Name)
                {
                    calendar.Rename(dto.Name, userId);
                }

                await calendarRepository.UpdateAsync(calendar, calendar.Version);
                applied.Add(new SyncAppliedChange(
                    "Calendar", calendar.Id, calendar.Version, calendar.UpdatedAt));
            }
            else if (change.Operation == SyncOperation.Delete)
            {
                var calendar = await calendarRepository.GetAsync(
                    dto.Id,
                    new CalendarQueryOptions { IncludeDeleted = true });

                if (calendar is null || calendar.IsDeleted)
                {
                    // Already deleted or doesn't exist — skip
                    return;
                }

                // Check version conflict
                if (change.ExpectedVersion.HasValue && calendar.Version != change.ExpectedVersion.Value)
                {
                    conflicts.Add(new SyncConflict(
                        "Calendar", calendar.Id, calendar.Version, calendar.UpdatedAt));
                    return;
                }

                calendar.SoftDelete(userId);
                await calendarRepository.UpdateAsync(calendar, calendar.Version);
                applied.Add(new SyncAppliedChange(
                    "Calendar", calendar.Id, calendar.Version, calendar.UpdatedAt));
            }
        }

        private static async Task ProcessScheduleItemChangeAsync(
            SyncScheduleItemChange change,
            Guid userId,
            ICalendarRepository calendarRepository,
            IScheduleItemRepository scheduleItemRepository,
            List<SyncAppliedChange> applied,
            List<SyncConflict> conflicts)
        {
            var dto = change.Entity;

            // Load parent calendar to verify permissions
            var calendar = await calendarRepository.GetAsync(
                dto.CalendarId,
                new CalendarQueryOptions { IncludeDeleted = true, IncludeMembers = true, IncludeScheduleItems = true });

            if (calendar is null)
            {
                // Calendar doesn't exist — skip
                return;
            }

            // Verify user is editor/owner
            var role = calendar.GetMemberRole(userId);
            if (role is null || role == CalendarMemberRole.Viewer)
            {
                throw new UnauthorizedAccessException(
                    "You do not have edit permissions on this calendar.");
            }

            if (change.Operation == SyncOperation.Upsert)
            {
                var existingItem = calendar.ScheduleItems
                    .FirstOrDefault(i => i.Id == dto.Id);

                if (existingItem is null || existingItem.IsDeleted)
                {
                    // Create new item (or restore then update)
                    ScheduleItem item;
                    if (existingItem is not null)
                    {
                        // Restore existing deleted item
                        calendar.RestoreScheduleItem(existingItem.Id, userId);
                        item = existingItem;
                        UpdateScheduleItemFromDto(item, dto);
                    }
                    else
                    {
                        item = CreateScheduleItemFromDto(dto, userId, calendar);
                        await scheduleItemRepository.AddNewItemTrackingAsync(item);
                    }

                    await calendarRepository.UpdateAsync(calendar, calendar.Version);
                    applied.Add(new SyncAppliedChange(
                        "ScheduleItem", item.Id, item.Version, item.UpdatedAt));
                    return;
                }

                // Check version conflict
                if (change.ExpectedVersion.HasValue && existingItem.Version != change.ExpectedVersion.Value)
                {
                    conflicts.Add(new SyncConflict(
                        "ScheduleItem", existingItem.Id, existingItem.Version, existingItem.UpdatedAt));
                    return;
                }

                // Update existing item
                UpdateScheduleItemFromDto(existingItem, dto);
                await calendarRepository.UpdateAsync(calendar, calendar.Version);
                applied.Add(new SyncAppliedChange(
                    "ScheduleItem", existingItem.Id, existingItem.Version, existingItem.UpdatedAt));
            }
            else if (change.Operation == SyncOperation.Delete)
            {
                var existingItem = calendar.ScheduleItems
                    .FirstOrDefault(i => i.Id == dto.Id && !i.IsDeleted);

                if (existingItem is null)
                {
                    // Already deleted or doesn't exist — skip
                    return;
                }

                // Check version conflict
                if (change.ExpectedVersion.HasValue && existingItem.Version != change.ExpectedVersion.Value)
                {
                    conflicts.Add(new SyncConflict(
                        "ScheduleItem", existingItem.Id, existingItem.Version, existingItem.UpdatedAt));
                    return;
                }

                calendar.DeleteScheduleItem(existingItem.Id, userId);
                await calendarRepository.UpdateAsync(calendar, calendar.Version);
                applied.Add(new SyncAppliedChange(
                    "ScheduleItem", existingItem.Id, existingItem.Version, existingItem.UpdatedAt));
            }
        }

        private static async Task ProcessAttachmentChangeAsync(
            SyncAttachmentChange change,
            Guid userId,
            ICalendarRepository calendarRepository,
            IAttachmentRepository attachmentRepository,
            List<SyncAppliedChange> applied,
            List<SyncConflict> conflicts)
        {
            var dto = change.Entity;

            // Load the parent schedule item's calendar to verify permissions
            // We need to find which calendar owns the schedule item that owns this attachment
            // Strategy: load calendars by member and find the one containing the schedule item
            var memberCalendars = await calendarRepository.ListByMemberAsync(
                userId,
                new CalendarQueryOptions { IncludeDeleted = true, IncludeScheduleItems = true });

            var calendar = memberCalendars
                .FirstOrDefault(c => c.ScheduleItems.Any(i => i.Id == dto.ScheduleItemId));

            if (calendar is null)
            {
                // Calendar not found or not accessible — skip
                return;
            }

            // Verify user is editor/owner
            var role = calendar.GetMemberRole(userId);
            if (role is null || role == CalendarMemberRole.Viewer)
            {
                throw new UnauthorizedAccessException(
                    "You do not have edit permissions on this calendar.");
            }

            var parentItem = calendar.ScheduleItems
                .FirstOrDefault(i => i.Id == dto.ScheduleItemId);

            if (parentItem is null)
            {
                return;
            }

            if (change.Operation == SyncOperation.Upsert)
            {
                var existingAttachment = parentItem.Attachments
                    .FirstOrDefault(a => a.Id == dto.Id);

                if (existingAttachment is null || existingAttachment.IsDeleted)
                {
                    // Create new attachment (or restore then update)
                    Attachment attachment;
                    if (existingAttachment is not null)
                    {
                        existingAttachment.Restore();
                        existingAttachment.UpdateDetails(
                            dto.FileName,
                            dto.ContentType,
                            dto.Size > 0 ? dto.Size : 1);
                        attachment = existingAttachment;
                    }
                    else
                    {
                        // Create with placeholder object key — client should have uploaded file already
                        attachment = Attachment.Create(
                            dto.ScheduleItemId,
                            dto.FileName,
                            dto.ContentType,
                            dto.Size > 0 ? dto.Size : 1,
                            $"attachments/{dto.ScheduleItemId}/{dto.Id}_{dto.FileName}");
                        await attachmentRepository.AddNewItemTrackingAsync(attachment);
                    }

                    await calendarRepository.UpdateAsync(calendar, calendar.Version);
                    applied.Add(new SyncAppliedChange(
                        "Attachment", attachment.Id, attachment.Version, attachment.UpdatedAt));
                    return;
                }

                // Check version conflict
                if (change.ExpectedVersion.HasValue && existingAttachment.Version != change.ExpectedVersion.Value)
                {
                    conflicts.Add(new SyncConflict(
                        "Attachment", existingAttachment.Id, existingAttachment.Version, existingAttachment.UpdatedAt));
                    return;
                }

                // Update existing attachment metadata
                existingAttachment.UpdateDetails(
                    dto.FileName,
                    dto.ContentType,
                    dto.Size > 0 ? dto.Size : 1);
                await calendarRepository.UpdateAsync(calendar, calendar.Version);
                applied.Add(new SyncAppliedChange(
                    "Attachment", existingAttachment.Id, existingAttachment.Version, existingAttachment.UpdatedAt));
            }
            else if (change.Operation == SyncOperation.Delete)
            {
                var existingAttachment = parentItem.Attachments
                    .FirstOrDefault(a => a.Id == dto.Id && !a.IsDeleted);

                if (existingAttachment is null)
                {
                    // Already deleted or doesn't exist — skip
                    return;
                }

                // Check version conflict
                if (change.ExpectedVersion.HasValue && existingAttachment.Version != change.ExpectedVersion.Value)
                {
                    conflicts.Add(new SyncConflict(
                        "Attachment", existingAttachment.Id, existingAttachment.Version, existingAttachment.UpdatedAt));
                    return;
                }

                existingAttachment.MarkDeleted(DateTimeOffset.UtcNow);
                await calendarRepository.UpdateAsync(calendar, calendar.Version);
                applied.Add(new SyncAppliedChange(
                    "Attachment", existingAttachment.Id, existingAttachment.Version, existingAttachment.UpdatedAt));
            }
        }

        private static ScheduleItem CreateScheduleItemFromDto(
            ScheduleItemDto dto, Guid userId, Calendar calendar)
        {
            if (dto.Type == "Event")
            {
                var evt = calendar.CreateEvent(
                    dto.Title,
                    dto.StartAt ?? DateTimeOffset.UtcNow,
                    dto.EndAt ?? DateTimeOffset.UtcNow.AddHours(1),
                    dto.AllDay,
                    userId,
                    dto.Description,
                    dto.Location);

                // Set recurrence rule if present
                if (dto.RecurrenceRule is not null)
                {
                    evt.SetRecurrenceRule(
                        dto.RecurrenceRule.Freq ?? RecurrenceFrequency.Daily,
                        dto.RecurrenceRule.Interval,
                        dto.RecurrenceRule.ByDay,
                        dto.RecurrenceRule.Until,
                        dto.RecurrenceRule.Count);
                }

                return evt;
            }
            else // "Task"
            {
                return calendar.CreateTask(
                    dto.Title,
                    userId,
                    dto.DueAt,
                    dto.Description,
                    dto.Location);
            }
        }

        private static void UpdateScheduleItemFromDto(ScheduleItem item, ScheduleItemDto dto)
        {
            // Update common fields
            item.UpdateDetails(dto.Title, dto.Description, dto.Location);

            if (item is ScheduleEvent evt)
            {
                // Update event-specific fields
                if (dto.StartAt.HasValue && dto.EndAt.HasValue)
                {
                    evt.Reschedule(dto.StartAt.Value, dto.EndAt.Value, dto.AllDay);
                }

                // Update recurrence rule
                if (dto.RecurrenceRule is not null)
                {
                    evt.SetRecurrenceRule(
                        dto.RecurrenceRule.Freq ?? RecurrenceFrequency.Daily,
                        dto.RecurrenceRule.Interval,
                        dto.RecurrenceRule.ByDay,
                        dto.RecurrenceRule.Until,
                        dto.RecurrenceRule.Count);
                }
                else if (evt.RecurrenceRule is not null)
                {
                    evt.ClearRecurrenceRule();
                }
            }
            else if (item is ScheduleTask task)
            {
                // Update task-specific fields
                task.SetDueAt(dto.DueAt);

                if (dto.IsCompleted && !task.IsCompleted)
                {
                    task.MarkComplete();
                }
                else if (!dto.IsCompleted && task.IsCompleted)
                {
                    task.Reopen();
                }
            }
        }
    }
}
