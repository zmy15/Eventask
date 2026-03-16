using Eventask.Domain.Entity.Users;
using FluentAssertions;

namespace Eventask.Domain.Tests.Entity.Users;

public class UserTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateUser()
    {
        // Arrange
        var username = "testuser";
        var passwordHash = "hashedpassword123";

        // Act
        var user = User.Create(username, passwordHash);

        // Assert
        user.Should().NotBeNull();
        user.Id.Should().NotBeEmpty();
        user.UserName.Should().Be(username);
        user.PasswordHash.Should().Be(passwordHash);
        user.IsDeleted.Should().BeFalse();
        user.DeletedAt.Should().BeNull();
        user.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidUsername_ShouldThrowArgumentException(string? invalidUsername)
    {
        // Arrange
        var passwordHash = "hashedpassword123";

        // Act
        var act = () => User.Create(invalidUsername!, passwordHash);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Username cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidPasswordHash_ShouldThrowArgumentException(string? invalidPasswordHash)
    {
        // Arrange
        var username = "testuser";

        // Act
        var act = () => User.Create(username, invalidPasswordHash!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Password hash cannot be empty*");
    }

    [Fact]
    public void Create_ShouldTrimUsername()
    {
        // Arrange
        var username = "  testuser  ";
        var passwordHash = "hashedpassword123";

        // Act
        var user = User.Create(username, passwordHash);

        // Assert
        user.UserName.Should().Be("testuser");
    }

    [Fact]
    public void ChangeUserName_WithValidName_ShouldUpdateUsername()
    {
        // Arrange
        var user = User.Create("oldname", "hash123");
        var oldUpdatedAt = user.UpdatedAt;
        Thread.Sleep(10); // Ensure time difference

        // Act
        user.ChangeUserName("newname");

        // Assert
        user.UserName.Should().Be("newname");
        user.UpdatedAt.Should().BeAfter(oldUpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeUserName_WithInvalidName_ShouldThrowArgumentException(string? invalidUsername)
    {
        // Arrange
        var user = User.Create("testuser", "hash123");

        // Act
        var act = () => user.ChangeUserName(invalidUsername!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Username cannot be empty*");
    }

    [Fact]
    public void ChangeUserName_ShouldTrimUsername()
    {
        // Arrange
        var user = User.Create("oldname", "hash123");

        // Act
        user.ChangeUserName("  newname  ");

        // Assert
        user.UserName.Should().Be("newname");
    }

    [Fact]
    public void ChangePasswordHash_WithValidHash_ShouldUpdatePasswordHash()
    {
        // Arrange
        var user = User.Create("testuser", "oldhash");
        var oldUpdatedAt = user.UpdatedAt;
        Thread.Sleep(10);

        // Act
        user.ChangePasswordHash("newhash");

        // Assert
        user.PasswordHash.Should().Be("newhash");
        user.UpdatedAt.Should().BeAfter(oldUpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePasswordHash_WithInvalidHash_ShouldThrowArgumentException(string? invalidHash)
    {
        // Arrange
        var user = User.Create("testuser", "hash123");

        // Act
        var act = () => user.ChangePasswordHash(invalidHash!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Password hash cannot be empty*");
    }

    [Fact]
    public void SoftDelete_ShouldMarkUserAsDeleted()
    {
        // Arrange
        var user = User.Create("testuser", "hash123");
        var updatedAtBeforeDelete = user.UpdatedAt;
        Thread.Sleep(10);

        // Act
        user.SoftDelete();

        // Assert
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
        user.DeletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        user.UpdatedAt.Should().BeAfter(updatedAtBeforeDelete);
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_ShouldNotChangeState()
    {
        // Arrange
        var user = User.Create("testuser", "hash123");
        user.SoftDelete();
        var deletedAt = user.DeletedAt;
        var updatedAt = user.UpdatedAt;
        Thread.Sleep(10);

        // Act
        user.SoftDelete();

        // Assert
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().Be(deletedAt);
        user.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Restore_ShouldRestoreDeletedUser()
    {
        // Arrange
        var user = User.Create("testuser", "hash123");
        user.SoftDelete();
        var updatedAtAfterDelete = user.UpdatedAt;
        Thread.Sleep(10);

        // Act
        user.Restore();

        // Assert
        user.IsDeleted.Should().BeFalse();
        user.DeletedAt.Should().BeNull();
        user.UpdatedAt.Should().BeAfter(updatedAtAfterDelete);
    }

    [Fact]
    public void Restore_WhenNotDeleted_ShouldNotChangeState()
    {
        // Arrange
        var user = User.Create("testuser", "hash123");
        var updatedAt = user.UpdatedAt;
        Thread.Sleep(10);

        // Act
        user.Restore();

        // Assert
        user.IsDeleted.Should().BeFalse();
        user.DeletedAt.Should().BeNull();
        user.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void CreateCalendar_ShouldCreateAndAddCalendar()
    {
        // Arrange
        var user = User.Create("testuser", "hash123");
        var calendarName = "My Calendar";
        var calendarColor = "#FF0000";

        // Act
        var calendar = user.CreateCalendar(calendarName, calendarColor);

        // Assert
        calendar.Should().NotBeNull();
        calendar.Name.Should().Be(calendarName);
        calendar.Color.Should().Be(calendarColor);
        calendar.OwnerId.Should().Be(user.Id);
        user.Calendars.Should().Contain(calendar);
    }
}
