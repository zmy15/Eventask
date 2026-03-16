using Eventask.Domain.Entity.Calendars;

namespace Eventask.Domain.Dtos;

public sealed record AuthResponse (
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string TokenType = "Bearer"
);

public sealed record CalendarDto (
    Guid Id,
    string Name,
    int Version,
    DateTimeOffset UpdatedAt,
    bool IsDeleted
);

public sealed record MemberDto (
    Guid Id,
    Guid UserId,
    string Username,
    CalendarMemberRole Role
);

public sealed record ScheduleItemDto (
    Guid Id,
    Guid CalendarId,
    string Type,
    string Title,
    string? Description,
    string? Location,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    DateTimeOffset? DueAt,
    bool AllDay,
    bool IsCompleted,
    DateTimeOffset? CompletedAt,
    int Version,
    DateTimeOffset UpdatedAt,
    bool IsDeleted
);

public sealed record AttachmentDto (
    Guid Id,
    Guid ScheduleItemId,
    string FileName,
    string ContentType,
    long Size,
    int Version,
    DateTimeOffset UpdatedAt,
    bool IsDeleted
);

public sealed record SyncPullResponse (
    DateTimeOffset ServerNow,
    SyncDelta Delta
);

public sealed record SyncPushResponse (
    DateTimeOffset ServerNow,
    SyncPushResult Result
);

public sealed record SyncDelta (
    IReadOnlyList<CalendarDto> Calendars,
    IReadOnlyList<ScheduleItemDto> Items,
    IReadOnlyList<AttachmentDto> Attachments
);

public sealed record SyncPushResult (
    IReadOnlyList<SyncAppliedChange> Applied,
    IReadOnlyList<SyncConflict> Conflicts
);

public sealed record SyncAppliedChange (
    string EntityType,
    Guid EntityId,
    int NewVersion,
    DateTimeOffset UpdatedAt
);

public sealed record SyncConflict (
    string EntityType,
    Guid EntityId,
    int ServerVersion,
    DateTimeOffset ServerUpdatedAt
);
