using Eventask.ApiService.Repository;
using Eventask.Domain.Entity.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Eventask.ApiService.Tests.Repository;

public class UserRepositoryTests : IDisposable
{
    private readonly EventaskContext _context;
    private readonly UserRepository _repository;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<EventaskContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EventaskContext(options);
        _repository = new UserRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ShouldAddUserToDatabase()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");

        // Act
        var result = await _repository.CreateAsync(user);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        var savedUser = await _context.Users.FindAsync(user.Id);
        savedUser.Should().NotBeNull();
        savedUser!.UserName.Should().Be("testuser");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateUserInDatabase()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");
        await _repository.CreateAsync(user);
        
        // Act
        user.ChangeUserName("newusername");
        var result = await _repository.UpdateAsync(user);

        // Assert
        result.UserName.Should().Be("newusername");
        var savedUser = await _context.Users.FindAsync(user.Id);
        savedUser!.UserName.Should().Be("newusername");
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ShouldReturnUser()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");
        await _repository.CreateAsync(user);

        // Act
        var result = await _repository.GetByIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.UserName.Should().Be("testuser");
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUsernameAsync_WhenUserExists_ShouldReturnUser()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");
        await _repository.CreateAsync(user);

        // Act
        var result = await _repository.GetByUsernameAsync("testuser");

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().Be("testuser");
    }

    [Fact]
    public async Task GetByUsernameAsync_WhenUserDoesNotExist_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByUsernameAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUsernameAsync_WhenUserIsDeleted_ShouldReturnNull()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");
        await _repository.CreateAsync(user);
        user.SoftDelete();
        await _repository.UpdateAsync(user);

        // Act
        var result = await _repository.GetByUsernameAsync("testuser");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsByUsernameAsync_WhenUserExists_ShouldReturnTrue()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");
        await _repository.CreateAsync(user);

        // Act
        var result = await _repository.ExistsByUsernameAsync("testuser");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByUsernameAsync_WhenUserDoesNotExist_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.ExistsByUsernameAsync("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByUsernameAsync_WhenUserIsDeleted_ShouldReturnFalse()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");
        await _repository.CreateAsync(user);
        user.SoftDelete();
        await _repository.UpdateAsync(user);

        // Act
        var result = await _repository.ExistsByUsernameAsync("testuser");

        // Assert
        result.Should().BeFalse();
    }
}
