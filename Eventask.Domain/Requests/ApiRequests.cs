using Eventask.Domain.Dtos;
using Eventask.Domain.Entity.Calendars;

namespace Eventask.Domain.Requests;

public sealed record RegisterRequest (
    string Username,
    string Password
);

public sealed record LoginRequest (
    string Username,
    string Password
);

public sealed record CreateCalendarRequest (
    string Name
);

public sealed record UpdateCalendarRequest (
    string Name
);

public sealed record CreateScheduleItemRequest (
    string Type,
    string Title,
    string? Description,
    string? Location,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    DateTimeOffset? DueAt,
    bool AllDay
);

public sealed record UpdateScheduleItemRequest (
    string Type,
    string Title,
    string? Description,
    string? Location,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    DateTimeOffset? DueAt,
    bool AllDay,
    bool IsCompleted
);

public sealed record AddCalendarMemberRequest (
    string Username,
    CalendarMemberRole Role
);

public sealed record SyncPullRequest (
    DateTimeOffset LastSyncAt
);

public sealed record SyncPushRequest (
    IReadOnlyList<SyncCalendarChange> Calendars,
    IReadOnlyList<SyncScheduleItemChange> Items,
    IReadOnlyList<SyncAttachmentChange> Attachments
);

public enum SyncOperation
{
    Upsert = 0,
    Delete = 1
}

public sealed record SyncCalendarChange (
    SyncOperation Operation,
    CalendarDto Entity,
    int? ExpectedVersion
);

public sealed record SyncScheduleItemChange (
    SyncOperation Operation,
    ScheduleItemDto Entity,
    int? ExpectedVersion
);

public sealed record SyncAttachmentChange (
    SyncOperation Operation,
    AttachmentDto Entity,
    int? ExpectedVersion
);
