using Amazon.S3;
using Eventask.ApiService.Services.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Eventask.ApiService.Tests.Services;

public class S3ObjectStorageServiceTests
{
    [Fact]
    public void Constructor_WithMissingEndpoint_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new ObjectStorageOptions
        {
            Endpoint = null,
            Bucket = "test-bucket",
            AccessKey = "access-key",
            SecretKey = "secret-key"
        };
        var mockOptions = new Mock<IOptions<ObjectStorageOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var act = () => new S3ObjectStorageService(mockOptions.Object);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*endpoint*");
    }

    [Fact]
    public void Constructor_WithMissingBucket_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new ObjectStorageOptions
        {
            Endpoint = "http://localhost:9000",
            Bucket = null,
            AccessKey = "access-key",
            SecretKey = "secret-key"
        };
        var mockOptions = new Mock<IOptions<ObjectStorageOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var act = () => new S3ObjectStorageService(mockOptions.Object);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bucket*");
    }

    [Fact]
    public void Constructor_WithMissingAccessKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new ObjectStorageOptions
        {
            Endpoint = "http://localhost:9000",
            Bucket = "test-bucket",
            AccessKey = null,
            SecretKey = "secret-key"
        };
        var mockOptions = new Mock<IOptions<ObjectStorageOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var act = () => new S3ObjectStorageService(mockOptions.Object);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials*");
    }

    [Fact]
    public void Constructor_WithMissingSecretKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new ObjectStorageOptions
        {
            Endpoint = "http://localhost:9000",
            Bucket = "test-bucket",
            AccessKey = "access-key",
            SecretKey = null
        };
        var mockOptions = new Mock<IOptions<ObjectStorageOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var act = () => new S3ObjectStorageService(mockOptions.Object);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials*");
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldCreateService()
    {
        // Arrange
        var options = new ObjectStorageOptions
        {
            Endpoint = "http://localhost:9000",
            Bucket = "test-bucket",
            AccessKey = "access-key",
            SecretKey = "secret-key",
            ForcePathStyle = true
        };
        var mockOptions = new Mock<IOptions<ObjectStorageOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        using var service = new S3ObjectStorageService(mockOptions.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ShouldDisposeClientSafely()
    {
        // Arrange
        var options = new ObjectStorageOptions
        {
            Endpoint = "http://localhost:9000",
            Bucket = "test-bucket",
            AccessKey = "access-key",
            SecretKey = "secret-key"
        };
        var mockOptions = new Mock<IOptions<ObjectStorageOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);
        var service = new S3ObjectStorageService(mockOptions.Object);

        // Act
        service.Dispose();

        // Assert - should not throw
        service.Dispose(); // Second dispose should be safe
    }
}
