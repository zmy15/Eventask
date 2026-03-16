using Eventask.Domain.Entity.Calendars;
using FluentAssertions;

namespace Eventask.Domain.Tests.Entity.Calendars;

public class CalendarMemberTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateCalendarMember()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var role = CalendarMemberRole.Editor;

        // Act
        var member = CalendarMember.Create(calendarId, userId, role);

        // Assert
        member.Should().NotBeNull();
        member.Id.Should().NotBeEmpty();
        member.CalendarId.Should().Be(calendarId);
        member.UserId.Should().Be(userId);
        member.Role.Should().Be(role);
        member.Version.Should().Be(0);
        member.IsDeleted.Should().BeFalse();
        member.DeletedAt.Should().BeNull();
        member.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(CalendarMemberRole.Owner)]
    [InlineData(CalendarMemberRole.Editor)]
    [InlineData(CalendarMemberRole.Viewer)]
    public void Create_WithAllRoles_ShouldCreateMemberWithRole(CalendarMemberRole role)
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var member = CalendarMember.Create(calendarId, userId, role);

        // Assert
        member.Role.Should().Be(role);
    }

    [Fact]
    public void ChangeRole_FromEditorToViewer_ShouldChangeRole()
    {
        // Arrange
        var member = CalendarMember.Create(Guid.NewGuid(), Guid.NewGuid(), CalendarMemberRole.Editor);
        var oldUpdatedAt = member.UpdatedAt;
        Thread.Sleep(10);

        // Act
        member.ChangeRole(CalendarMemberRole.Viewer);

        // Assert
        member.Role.Should().Be(CalendarMemberRole.Viewer);
        member.UpdatedAt.Should().BeAfter(oldUpdatedAt);
    }

    [Fact]
    public void ChangeRole_FromViewerToEditor_ShouldChangeRole()
    {
        // Arrange
        var member = CalendarMember.Create(Guid.NewGuid(), Guid.NewGuid(), CalendarMemberRole.Viewer);

        // Act
        member.ChangeRole(CalendarMemberRole.Editor);

        // Assert
        member.Role.Should().Be(CalendarMemberRole.Editor);
    }

    [Fact]
    public void ChangeRole_ForOwner_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var member = CalendarMember.Create(Guid.NewGuid(), Guid.NewGuid(), CalendarMemberRole.Owner);

        // Act
        var act = () => member.ChangeRole(CalendarMemberRole.Editor);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot change the owner's role*");
    }

    [Fact]
    public void ChangeRole_ToOwner_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var member = CalendarMember.Create(Guid.NewGuid(), Guid.NewGuid(), CalendarMemberRole.Editor);

        // Act
        var act = () => member.ChangeRole(CalendarMemberRole.Owner);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot promote to owner*");
    }

    [Fact]
    public void MarkDeleted_ShouldMarkMemberAsDeleted()
    {
        // Arrange
        var member = CalendarMember.Create(Guid.NewGuid(), Guid.NewGuid(), CalendarMemberRole.Editor);
        var deletedAt = DateTimeOffset.UtcNow;
        var oldUpdatedAt = member.UpdatedAt;
        Thread.Sleep(10);

        // Act
        member.MarkDeleted(deletedAt);

        // Assert
        member.IsDeleted.Should().BeTrue();
        member.DeletedAt.Should().Be(deletedAt);
        member.UpdatedAt.Should().BeAfter(oldUpdatedAt);
    }

    [Fact]
    public void MarkDeleted_WhenAlreadyDeleted_ShouldNotChangeState()
    {
        // Arrange
        var member = CalendarMember.Create(Guid.NewGuid(), Guid.NewGuid(), CalendarMemberRole.Editor);
        var firstDeletedAt = DateTimeOffset.UtcNow;
        member.MarkDeleted(firstDeletedAt);
        var updatedAt = member.UpdatedAt;
        Thread.Sleep(10);

        // Act
        var secondDeletedAt = DateTimeOffset.UtcNow;
        member.MarkDeleted(secondDeletedAt);

        // Assert
        member.IsDeleted.Should().BeTrue();
        member.DeletedAt.Should().Be(firstDeletedAt);
        member.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Restore_ShouldRestoreDeletedMember()
    {
        // Arrange
        var member = CalendarMember.Create(Guid.NewGuid(), Guid.NewGuid(), CalendarMemberRole.Editor);
        member.MarkDeleted(DateTimeOffset.UtcNow);
        var updatedAtAfterDelete = member.UpdatedAt;
        Thread.Sleep(10);

        // Act
        member.Restore();

        // Assert
        member.IsDeleted.Should().BeFalse();
        member.DeletedAt.Should().BeNull();
        member.UpdatedAt.Should().BeAfter(updatedAtAfterDelete);
    }

    [Fact]
    public void Restore_WhenNotDeleted_ShouldNotChangeState()
    {
        // Arrange
        var member = CalendarMember.Create(Guid.NewGuid(), Guid.NewGuid(), CalendarMemberRole.Editor);
        var updatedAt = member.UpdatedAt;
        Thread.Sleep(10);

        // Act
        member.Restore();

        // Assert
        member.IsDeleted.Should().BeFalse();
        member.DeletedAt.Should().BeNull();
        member.UpdatedAt.Should().Be(updatedAt);
    }
}
