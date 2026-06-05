using Eventask.Domain.Contracts;

namespace Eventask.Domain.Entity.Calendars.ScheduleItems;

public class Attachment : ISynchronizableEntity
{
    public Guid Id { get; } = Guid.NewGuid();

    public Guid ScheduleItemId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public string ObjectKey { get; private set; } = string.Empty;

    public string? Sha256 { get; private set; }

    public int Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ScheduleItem? ScheduleItem { get; set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private Attachment ( ) { }

    /// <summary>
    /// Factory method to create a new Attachment.
    /// </summary>
    public static Attachment Create (Guid scheduleItemId, string fileName, string contentType, long size, string objectKey, string? sha256 = null)
    {
        if ( string.IsNullOrWhiteSpace(fileName) )
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
        if ( string.IsNullOrWhiteSpace(objectKey) )
            throw new ArgumentException("Object key cannot be empty.", nameof(objectKey));
        if ( size <= 0 )
            throw new ArgumentException("Size must be greater than zero.", nameof(size));

        var now = DateTimeOffset.UtcNow;
        return new Attachment
        {
            ScheduleItemId = scheduleItemId,
            FileName = fileName.Trim(),
            ContentType = contentType,
            Size = size,
            ObjectKey = objectKey,
            Sha256 = sha256,
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

    /// <summary>
    /// 更新附件元数据（文件名、类型、大小等）。ObjectKey 和 Sha256 为可选参数，
    /// 仅当传入非 null 值时才会更新对应字段。用于 Sync Push 场景更新已存在的附件。
    /// </summary>
    public void UpdateDetails(string fileName, string contentType, long size, string? objectKey = null, string? sha256 = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
        if (size <= 0)
            throw new ArgumentException("Size must be greater than zero.", nameof(size));

        FileName = fileName.Trim();
        ContentType = contentType;
        Size = size;
        if (objectKey is not null)
            ObjectKey = objectKey;
        if (sha256 is not null)
            Sha256 = sha256;
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


public interface IAttachmentRepository
{
    Task AddNewItemTrackingAsync (Attachment item);

    /// <summary>
    /// Returns all attachments (including deleted tombstone records) that were updated
    /// after the specified timestamp, filtered by the given schedule item IDs,
    /// ordered by UpdatedAt, with pagination. Used by Sync Pull.
    /// </summary>
    Task<IReadOnlyList<Attachment>> ListChangedSinceAsync(
        IReadOnlyList<Guid> scheduleItemIds, DateTimeOffset changedSince, int pageSize,
        CancellationToken cancellationToken = default);
}
