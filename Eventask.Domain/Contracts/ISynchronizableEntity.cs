namespace Eventask.Domain.Contracts;

public interface ISynchronizableEntity
{
    int Version { get; set; }

    DateTimeOffset UpdatedAt { get; set; }

    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAt { get; set; }
}
