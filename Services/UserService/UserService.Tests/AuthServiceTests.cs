using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Common.Messaging;
using MessageContracts.Events.User;
using MessageContracts.Enums;
using UserService.API.Services;
using UserService.API.Models;
using UserService.API.Models.Dtos;
using UserService.API.Data; // For UserDbContext

public class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<UserDbContext> _mockDbContext; // Or use InMemory
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IEventPublisher> _mockEventPublisher;
    private readonly Mock<ILogger<AuthService>> _mockLogger;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        // UserManager
        var userStore = new MockUserStore(); // Using the same MockUserStore from UserServiceTests
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        // UserDbContext - Using InMemory for simplicity for some Identity operations if needed,
        // but mostly relying on UserManager mocks for AuthService.
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique name for each test run
            .Options;
        _mockDbContext = new Mock<UserDbContext>(options); // Mocking DbContext directly if not using InMemory for specific tests

        // Configuration
        _mockConfiguration = new Mock<IConfiguration>();
        // Setup mock configuration for JWT settings if LoginAsync actually generates a token
        // For this test, we'll assume token generation is out of scope for unit testing the event publishing part.
        _mockConfiguration.SetupGet(x => x["Jwt:Key"]).Returns("your-super-secret-jwt-key-with-at-least-256-bits");
        _mockConfiguration.SetupGet(x => x["Jwt:Issuer"]).Returns("your-issuer");
        _mockConfiguration.SetupGet(x => x["Jwt:Audience"]).Returns("your-audience");
        _mockConfiguration.SetupGet(x => x["Jwt:DurationInMinutes"]).Returns("60");


        _mockEventPublisher = new Mock<IEventPublisher>();
        _mockLogger = new Mock<ILogger<AuthService>>();

        _authService = new AuthService(
            _mockUserManager.Object,
            _mockConfiguration.Object,
            _mockEventPublisher.Object,
            _mockLogger.Object,
            _mockDbContext.Object  // Pass the DbContext mock
        );
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_PublishesUserLoginEventAndReturnsToken()
    {
        // Arrange
        var loginRequest = new LoginRequest { Email = "test@example.com", Password = "Password123!" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = loginRequest.Email, UserName = loginRequest.Email, Role = UserRole.Shipper, IsEmailConfirmed = true };

        _mockUserManager.Setup(um => um.FindByEmailAsync(loginRequest.Email)).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.CheckPasswordAsync(user, loginRequest.Password)).ReturnsAsync(true);
        _mockUserManager.Setup(um => um.GetRolesAsync(user)).ReturnsAsync(new List<string> { user.Role.ToString() });


        UserLoginEvent? publishedEvent = null;
        _mockEventPublisher.Setup(ep => ep.PublishAsync(It.IsAny<UserLoginEvent>()))
            .Callback<UserLoginEvent>(e => publishedEvent = e)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.LoginAsync(loginRequest);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token); // Token should be generated
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserLoginEvent>()), Times.Once);
        Assert.NotNull(publishedEvent);
        Assert.Equal(user.Id, publishedEvent?.UserId);
        Assert.Equal(user.Email, publishedEvent?.Email);
    }

    [Fact]
    public async Task LoginAsync_InvalidEmail_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var loginRequest = new LoginRequest { Email = "wrong@example.com", Password = "Password123!" };
        _mockUserManager.Setup(um => um.FindByEmailAsync(loginRequest.Email)).ReturnsAsync((ApplicationUser)null!);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(loginRequest));
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserLoginEvent>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_IncorrectPassword_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var loginRequest = new LoginRequest { Email = "test@example.com", Password = "WrongPassword!" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = loginRequest.Email };

        _mockUserManager.Setup(um => um.FindByEmailAsync(loginRequest.Email)).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.CheckPasswordAsync(user, loginRequest.Password)).ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(loginRequest));
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserLoginEvent>()), Times.Never);
    }
    
    [Fact]
    public async Task LoginAsync_UserNotActive_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var loginRequest = new LoginRequest { Email = "test@example.com", Password = "Password123!" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = loginRequest.Email, UserName = loginRequest.Email, Status = UserStatus.PendingVerification }; // Not Active

        _mockUserManager.Setup(um => um.FindByEmailAsync(loginRequest.Email)).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.CheckPasswordAsync(user, loginRequest.Password)).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(loginRequest));
        Assert.Equal("User account is not active. Please verify your email or contact support.", ex.Message);
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserLoginEvent>()), Times.Never);
    }


    [Fact]
    public async Task VerifyEmailAsync_ValidToken_ConfirmsEmailAndPublishesEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "valid_token";
        var user = new ApplicationUser { Id = userId, Email = "test@example.com", IsEmailConfirmed = false, Role = UserRole.Driver };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.ConfirmEmailAsync(user, token)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success); // For setting IsEmailConfirmed = true

        UserVerifiedEvent? publishedEvent = null;
        _mockEventPublisher.Setup(ep => ep.PublishAsync(It.IsAny<UserVerifiedEvent>()))
            .Callback<UserVerifiedEvent>(e => publishedEvent = e)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.VerifyEmailAsync(userId, token);

        // Assert
        Assert.True(result);
        Assert.True(user.IsEmailConfirmed);
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserVerifiedEvent>()), Times.Once);
        Assert.NotNull(publishedEvent);
        Assert.Equal(userId, publishedEvent?.UserId);
        Assert.True(publishedEvent?.IsEmailConfirmed);
        Assert.Equal(user.IsPhoneConfirmed, publishedEvent?.IsPhoneConfirmed);
    }

    [Fact]
    public async Task VerifyEmailAsync_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "any_token";
        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync((ApplicationUser)null!);

        // Act
        var result = await _authService.VerifyEmailAsync(userId, token);

        // Assert
        Assert.False(result);
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserVerifiedEvent>()), Times.Never);
    }

    [Fact]
    public async Task VerifyEmailAsync_InvalidToken_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "invalid_token";
        var user = new ApplicationUser { Id = userId, Email = "test@example.com", IsEmailConfirmed = false };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.ConfirmEmailAsync(user, token)).ReturnsAsync(IdentityResult.Failed());

        // Act
        var result = await _authService.VerifyEmailAsync(userId, token);

        // Assert
        Assert.False(result);
        Assert.False(user.IsEmailConfirmed);
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserVerifiedEvent>()), Times.Never);
    }
}
