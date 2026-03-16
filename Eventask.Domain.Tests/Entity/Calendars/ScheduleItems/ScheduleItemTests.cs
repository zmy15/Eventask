using Eventask.Domain.Entity.Calendars.ScheduleItems;
using FluentAssertions;

namespace Eventask.Domain.Tests.Entity.Calendars.ScheduleItems;

public class ScheduleEventTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateScheduleEvent()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        var title = "Team Meeting";
        var startAt = DateTimeOffset.UtcNow;
        var endAt = startAt.AddHours(1);
        var allDay = false;
        var description = "Discuss project";
        var location = "Conference Room";

        // Act
        var scheduleEvent = ScheduleEvent.Create(calendarId, title, startAt, endAt, allDay, description, location);

        // Assert
        scheduleEvent.Should().NotBeNull();
        scheduleEvent.Id.Should().NotBeEmpty();
        scheduleEvent.CalendarId.Should().Be(calendarId);
        scheduleEvent.Title.Should().Be(title);
        scheduleEvent.StartAt.Should().Be(startAt);
        scheduleEvent.EndAt.Should().Be(endAt);
        scheduleEvent.AllDay.Should().Be(allDay);
        scheduleEvent.Description.Should().Be(description);
        scheduleEvent.Location.Should().Be(location);
        scheduleEvent.Version.Should().Be(0);
        scheduleEvent.IsDeleted.Should().BeFalse();
        scheduleEvent.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidTitle_ShouldThrowArgumentException(string? invalidTitle)
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        var startAt = DateTimeOffset.UtcNow;
        var endAt = startAt.AddHours(1);

        // Act
        var act = () => ScheduleEvent.Create(calendarId, invalidTitle!, startAt, endAt, false);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void Create_WhenEndAtBeforeStartAt_ShouldThrowArgumentException()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        var startAt = DateTimeOffset.UtcNow;
        var endAt = startAt.AddHours(-1); // End before start

        // Act
        var act = () => ScheduleEvent.Create(calendarId, "Meeting", startAt, endAt, false);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EndAt must be after StartAt*");
    }

    [Fact]
    public void Create_WhenEndAtEqualsStartAt_ShouldThrowArgumentException()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        var startAt = DateTimeOffset.UtcNow;
        var endAt = startAt; // End equals start

        // Act
        var act = () => ScheduleEvent.Create(calendarId, "Meeting", startAt, endAt, false);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EndAt must be after StartAt*");
    }

    [Fact]
    public void Create_ShouldTrimTitle()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        var title = "  Meeting  ";
        var startAt = DateTimeOffset.UtcNow;
        var endAt = startAt.AddHours(1);

        // Act
        var scheduleEvent = ScheduleEvent.Create(calendarId, title, startAt, endAt, false);

        // Assert
        scheduleEvent.Title.Should().Be("Meeting");
    }

    [Fact]
    public void Reschedule_WithValidDates_ShouldUpdateSchedule()
    {
        // Arrange
        var scheduleEvent = ScheduleEvent.Create(
            Guid.NewGuid(),
            "Meeting",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            false);
        var oldUpdatedAt = scheduleEvent.UpdatedAt;
        Thread.Sleep(10);

        var newStartAt = DateTimeOffset.UtcNow.AddDays(1);
        var newEndAt = newStartAt.AddHours(2);

        // Act
        scheduleEvent.Reschedule(newStartAt, newEndAt, true);

        // Assert
        scheduleEvent.StartAt.Should().Be(newStartAt);
        scheduleEvent.EndAt.Should().Be(newEndAt);
        scheduleEvent.AllDay.Should().BeTrue();
        scheduleEvent.UpdatedAt.Should().BeAfter(oldUpdatedAt);
    }

    [Fact]
    public void Reschedule_WhenNewEndAtBeforeNewStartAt_ShouldThrowArgumentException()
    {
        // Arrange
        var scheduleEvent = ScheduleEvent.Create(
            Guid.NewGuid(),
            "Meeting",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            false);

        var newStartAt = DateTimeOffset.UtcNow.AddDays(1);
        var newEndAt = newStartAt.AddHours(-1);

        // Act
        var act = () => scheduleEvent.Reschedule(newStartAt, newEndAt, false);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EndAt must be after StartAt*");
    }

    [Fact]
    public void UpdateDetails_WithValidData_ShouldUpdateDetails()
    {
        // Arrange
        var scheduleEvent = ScheduleEvent.Create(
            Guid.NewGuid(),
            "Old Title",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            false,
            "Old Description",
            "Old Location");
        var oldUpdatedAt = scheduleEvent.UpdatedAt;
        Thread.Sleep(10);

        // Act
        scheduleEvent.UpdateDetails("New Title", "New Description", "New Location");

        // Assert
        scheduleEvent.Title.Should().Be("New Title");
        scheduleEvent.Description.Should().Be("New Description");
        scheduleEvent.Location.Should().Be("New Location");
        scheduleEvent.UpdatedAt.Should().BeAfter(oldUpdatedAt);
    }

    [Fact]
    public void MarkDeleted_ShouldMarkEventAndCascadeToRemindersAndAttachments()
    {
        // Arrange
        var scheduleEvent = ScheduleEvent.Create(
            Guid.NewGuid(),
            "Meeting",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            false);
        scheduleEvent.AddReminder(15);
        scheduleEvent.AddAttachment("file.pdf", "application/pdf", 1024, "objectkey123");
        var deletedAt = DateTimeOffset.UtcNow;

        // Act
        scheduleEvent.MarkDeleted(deletedAt);

        // Assert
        scheduleEvent.IsDeleted.Should().BeTrue();
        scheduleEvent.DeletedAt.Should().Be(deletedAt);
        scheduleEvent.Reminders.Should().AllSatisfy(r => r.IsDeleted.Should().BeTrue());
        scheduleEvent.Attachments.Should().AllSatisfy(a => a.IsDeleted.Should().BeTrue());
    }

    [Fact]
    public void Restore_ShouldRestoreDeletedEvent()
    {
        // Arrange
        var scheduleEvent = ScheduleEvent.Create(
            Guid.NewGuid(),
            "Meeting",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            false);
        scheduleEvent.MarkDeleted(DateTimeOffset.UtcNow);

        // Act
        scheduleEvent.Restore();

        // Assert
        scheduleEvent.IsDeleted.Should().BeFalse();
        scheduleEvent.DeletedAt.Should().BeNull();
    }
}

public class ScheduleTaskTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateScheduleTask()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        var title = "Buy groceries";
        var dueAt = DateTimeOffset.UtcNow.AddDays(1);
        var description = "Milk, bread, eggs";
        var location = "Supermarket";

        // Act
        var scheduleTask = ScheduleTask.Create(calendarId, title, dueAt, description, location);

        // Assert
        scheduleTask.Should().NotBeNull();
        scheduleTask.Id.Should().NotBeEmpty();
        scheduleTask.CalendarId.Should().Be(calendarId);
        scheduleTask.Title.Should().Be(title);
        scheduleTask.DueAt.Should().Be(dueAt);
        scheduleTask.Description.Should().Be(description);
        scheduleTask.Location.Should().Be(location);
        scheduleTask.IsCompleted.Should().BeFalse();
        scheduleTask.CompletedAt.Should().BeNull();
        scheduleTask.Version.Should().Be(0);
        scheduleTask.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidTitle_ShouldThrowArgumentException(string? invalidTitle)
    {
        // Arrange
        var calendarId = Guid.NewGuid();

        // Act
        var act = () => ScheduleTask.Create(calendarId, invalidTitle!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void Create_WithoutDueAt_ShouldCreateTaskWithNullDueAt()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        var title = "Task without due date";

        // Act
        var scheduleTask = ScheduleTask.Create(calendarId, title);

        // Assert
        scheduleTask.DueAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimTitle()
    {
        // Arrange
        var calendarId = Guid.NewGuid();
        var title = "  Buy groceries  ";

        // Act
        var scheduleTask = ScheduleTask.Create(calendarId, title);

        // Assert
        scheduleTask.Title.Should().Be("Buy groceries");
    }

    [Fact]
    public void SetDueAt_ShouldUpdateDueAt()
    {
        // Arrange
        var scheduleTask = ScheduleTask.Create(Guid.NewGuid(), "Task");
        var oldUpdatedAt = scheduleTask.UpdatedAt;
        Thread.Sleep(10);
        var newDueAt = DateTimeOffset.UtcNow.AddDays(2);

        // Act
        scheduleTask.SetDueAt(newDueAt);

        // Assert
        scheduleTask.DueAt.Should().Be(newDueAt);
        scheduleTask.UpdatedAt.Should().BeAfter(oldUpdatedAt);
    }

    [Fact]
    public void SetDueAt_ToNull_ShouldClearDueAt()
    {
        // Arrange
        var scheduleTask = ScheduleTask.Create(Guid.NewGuid(), "Task", DateTimeOffset.UtcNow.AddDays(1));

        // Act
        scheduleTask.SetDueAt(null);

        // Assert
        scheduleTask.DueAt.Should().BeNull();
    }

    [Fact]
    public void MarkComplete_ShouldMarkTaskAsCompleted()
    {
        // Arrange
        var scheduleTask = ScheduleTask.Create(Guid.NewGuid(), "Task");
        var oldUpdatedAt = scheduleTask.UpdatedAt;
        Thread.Sleep(10);

        // Act
        scheduleTask.MarkComplete();

        // Assert
        scheduleTask.IsCompleted.Should().BeTrue();
        scheduleTask.CompletedAt.Should().NotBeNull();
        scheduleTask.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        scheduleTask.UpdatedAt.Should().BeAfter(oldUpdatedAt);
    }

    [Fact]
    public void MarkComplete_WhenAlreadyCompleted_ShouldNotChangeState()
    {
        // Arrange
        var scheduleTask = ScheduleTask.Create(Guid.NewGuid(), "Task");
        scheduleTask.MarkComplete();
        var completedAt = scheduleTask.CompletedAt;
        var updatedAt = scheduleTask.UpdatedAt;
        Thread.Sleep(10);

        // Act
        scheduleTask.MarkComplete();

        // Assert
        scheduleTask.IsCompleted.Should().BeTrue();
        scheduleTask.CompletedAt.Should().Be(completedAt);
        scheduleTask.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Reopen_ShouldReopenCompletedTask()
    {
        // Arrange
        var scheduleTask = ScheduleTask.Create(Guid.NewGuid(), "Task");
        scheduleTask.MarkComplete();
        var updatedAtAfterComplete = scheduleTask.UpdatedAt;
        Thread.Sleep(10);

        // Act
        scheduleTask.Reopen();

        // Assert
        scheduleTask.IsCompleted.Should().BeFalse();
        scheduleTask.CompletedAt.Should().BeNull();
        scheduleTask.UpdatedAt.Should().BeAfter(updatedAtAfterComplete);
    }

    [Fact]
    public void Reopen_WhenNotCompleted_ShouldNotChangeState()
    {
        // Arrange
        var scheduleTask = ScheduleTask.Create(Guid.NewGuid(), "Task");
        var updatedAt = scheduleTask.UpdatedAt;
        Thread.Sleep(10);

        // Act
        scheduleTask.Reopen();

        // Assert
        scheduleTask.IsCompleted.Should().BeFalse();
        scheduleTask.CompletedAt.Should().BeNull();
        scheduleTask.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void MarkDeleted_ShouldMarkTaskAsDeleted()
    {
        // Arrange
        var scheduleTask = ScheduleTask.Create(Guid.NewGuid(), "Task");
        var deletedAt = DateTimeOffset.UtcNow;

        // Act
        scheduleTask.MarkDeleted(deletedAt);

        // Assert
        scheduleTask.IsDeleted.Should().BeTrue();
        scheduleTask.DeletedAt.Should().Be(deletedAt);
    }
}
