using Eventask.ApiService.Services.Auth;
using Eventask.Domain.Entity.Users;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Eventask.ApiService.Tests.Services;

public class JwtServiceTests
{
    private readonly JwtSettings _jwtSettings;
    private readonly JwtService _jwtService;

    public JwtServiceTests()
    {
        _jwtSettings = new JwtSettings
        {
            SecretKey = "ThisIsAVeryLongSecretKeyForTestingPurposesOnly1234567890",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 60
        };

        var mockOptions = new Mock<IOptions<JwtSettings>>();
        mockOptions.Setup(x => x.Value).Returns(_jwtSettings);

        _jwtService = new JwtService(mockOptions.Object);
    }

    [Fact]
    public void GenerateToken_ShouldReturnValidToken()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");

        // Act
        var (token, expiresAt) = _jwtService.GenerateToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        expiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void GenerateToken_ShouldIncludeUserIdInClaims()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");

        // Act
        var (token, _) = _jwtService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        subClaim.Should().NotBeNull();
        subClaim!.Value.Should().Be(user.Id.ToString());
    }

    [Fact]
    public void GenerateToken_ShouldIncludeUsernameInClaims()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");

        // Act
        var (token, _) = _jwtService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name);
        nameClaim.Should().NotBeNull();
        nameClaim!.Value.Should().Be("testuser");
    }

    [Fact]
    public void GenerateToken_ShouldIncludeJtiClaim()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");

        // Act
        var (token, _) = _jwtService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        var jtiClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        jtiClaim.Should().NotBeNull();
        jtiClaim!.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_ShouldSetCorrectIssuer()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");

        // Act
        var (token, _) = _jwtService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Issuer.Should().Be(_jwtSettings.Issuer);
    }

    [Fact]
    public void GenerateToken_ShouldSetCorrectAudience()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");

        // Act
        var (token, _) = _jwtService.GenerateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Audiences.Should().Contain(_jwtSettings.Audience);
    }

    [Fact]
    public void GenerateToken_ShouldSetCorrectExpiration()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");
        var expectedExpiration = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

        // Act
        var (token, expiresAt) = _jwtService.GenerateToken(user);

        // Assert
        expiresAt.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
        
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiration.UtcDateTime, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateToken_ForDifferentUsers_ShouldGenerateDifferentTokens()
    {
        // Arrange
        var user1 = User.Create("user1", "hash1");
        var user2 = User.Create("user2", "hash2");

        // Act
        var (token1, _) = _jwtService.GenerateToken(user1);
        var (token2, _) = _jwtService.GenerateToken(user2);

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateToken_CalledTwiceForSameUser_ShouldGenerateDifferentTokens()
    {
        // Arrange
        var user = User.Create("testuser", "hashedpassword");

        // Act
        var (token1, _) = _jwtService.GenerateToken(user);
        var (token2, _) = _jwtService.GenerateToken(user);

        // Assert
        // Tokens should be different because of different JTI (unique token ID)
        token1.Should().NotBe(token2);
    }
}
