using Eventask.Domain.Entity.Calendars;

namespace Eventask.Domain.Entity.Users;

public class User
{
    public Guid Id { get; private set; }

    public string UserName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<Calendar> Calendars { get; private set; } = new List<Calendar>();

    public ICollection<CalendarMember> CalendarMemberships { get; private set; } = new List<CalendarMember>();

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private User() { }

    /// <summary>
    /// Factory method to create a new User.
    /// </summary>
    public static User Create(string userName, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("Username cannot be empty.", nameof(userName));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));

        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            UserName = userName.Trim(),
            PasswordHash = passwordHash,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    private void BumpUpdatedAt()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeUserName(string newUserName)
    {
        if (string.IsNullOrWhiteSpace(newUserName))
            throw new ArgumentException("Username cannot be empty.", nameof(newUserName));
        UserName = newUserName.Trim();
        BumpUpdatedAt();
    }

    public void ChangePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(newPasswordHash));
        PasswordHash = newPasswordHash;
        BumpUpdatedAt();
    }

    public void SoftDelete()
    {
        if (IsDeleted)
            return;
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        BumpUpdatedAt();
    }

    public void Restore()
    {
        if (!IsDeleted)
            return;
        IsDeleted = false;
        DeletedAt = null;
        BumpUpdatedAt();
    }

    /// <summary>
    /// Creates a new Calendar owned by this user and adds it to the user's calendars.
    /// </summary>
    public Calendar CreateCalendar(string name, string? color = null)
    {
        var calendar = Calendar.Create(Id, name, color);
        Calendars.Add(calendar);
        return calendar;
    }
}

public interface IUserRepository
{
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task<User?> GetByIdAsync(Guid userId);
    Task<User?> GetByUsernameAsync(string username);
    Task<bool> ExistsByUsernameAsync(string username);
}
