using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using MessageContracts.Events.User;
using MessageContracts.Enums;
using UserService.API.Services;
using UserService.API.Models;
using UserService.API.Models.Dtos;
using UserService.API.Models.Enums; // For UserStatus enum

// Mocking UserManager requires a custom UserStore
public class MockUserStore : IUserStore<ApplicationUser>, IUserEmailStore<ApplicationUser>, IUserPasswordStore<ApplicationUser>, IUserRoleStore<ApplicationUser>, IUserPhoneNumberStore<ApplicationUser>
{
    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
    public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken) { user.Email = email; return Task.CompletedTask; }
    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);
    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.IsEmailConfirmed);
    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken) { user.IsEmailConfirmed = confirmed; return Task.CompletedTask; }
    public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);
    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken) { user.NormalizedEmail = normalizedEmail; return Task.CompletedTask; }
    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken) { user.PasswordHash = passwordHash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash);
    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash != null);
    public Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult<IList<string>>(new List<string> { user.Role.ToString() });
    public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken) => Task.FromResult(user.Role.ToString() == roleName);
    public Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken) => Task.FromResult<IList<ApplicationUser>>(new List<ApplicationUser>());
    public void Dispose() { } // Required by IUserStore
    public Task SetPhoneNumberAsync(ApplicationUser user, string? phoneNumber, CancellationToken cancellationToken) { user.PhoneNumber = phoneNumber; return Task.CompletedTask; }
    public Task<string?> GetPhoneNumberAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PhoneNumber);
    public Task<bool> GetPhoneNumberConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.IsPhoneConfirmed);
    public Task SetPhoneNumberConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken) { user.IsPhoneConfirmed = confirmed; return Task.CompletedTask; }
}

public class UserServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<IEventPublisher> _mockEventPublisher;
    private readonly Mock<ILogger<UserService.API.Services.UserService>> _mockLogger;
    private readonly UserService.API.Services.UserService _userService;

    public UserServiceTests()
    {
        // UserManager's constructor requires an IUserStore, IOptions<IdentityOptions>, IPasswordHasher, 
        // IEnumerable<IUserValidator>, IEnumerable<IPasswordValidator>, ILookupNormalizer, 
        // IdentityErrorDescriber, IServiceProvider, and ILogger.
        // For simplicity, we mock the store and use nulls or basic mocks for others.
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        // To fully mock UserManager, you might need to delve deeper into its dependencies.
        // Often, it's easier to use an in-memory database for Identity tests if you have a DbContext.
        // However, the prompt specifically asks for mocking UserManager.
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockAuthService = new Mock<IAuthService>();
        _mockEventPublisher = new Mock<IEventPublisher>();
        _mockLogger = new Mock<ILogger<UserService.API.Services.UserService>>();

        _userService = new UserService.API.Services.UserService(
            _mockUserManager.Object,
            _mockHttpContextAccessor.Object,
            _mockAuthService.Object,
            _mockEventPublisher.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task RegisterAsync_SuccessfulRegistration_PublishesUserCreatedEvent()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
            Name = "Test User",
            Role = UserRole.Shipper
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(registerRequest.Email))
            .ReturnsAsync((ApplicationUser)null!); // User does not exist

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<ApplicationUser>(), registerRequest.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((user, pass) => user.Id = Guid.NewGuid()); // Simulate ID generation

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        
        _mockAuthService.Setup(auth => auth.GenerateEmailVerificationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("dummy_token");

        UserCreatedEvent? publishedEvent = null;
        _mockEventPublisher.Setup(ep => ep.PublishAsync(It.IsAny<UserCreatedEvent>()))
            .Callback<UserCreatedEvent>(e => publishedEvent = e)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.RegisterAsync(registerRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(registerRequest.Email, result.Email);
        Assert.Equal(registerRequest.Name, result.Name);
        Assert.Equal(registerRequest.Role, result.Role);
        //Assert.True(result.IsEmailConfirmed); // Per DEV MODE comment in UserService
        //Assert.True(result.IsPhoneConfirmed); // Per DEV MODE comment in UserService
        //Assert.Equal(UserStatus.Active, result.Status); // Per DEV MODE comment

        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserCreatedEvent>()), Times.Once);
        Assert.NotNull(publishedEvent);
        Assert.Equal(registerRequest.Email, publishedEvent?.Email);
        Assert.Equal(registerRequest.Name, publishedEvent?.Name);
        Assert.Equal((MessageContracts.Enums.UserRole)registerRequest.Role, publishedEvent?.Role);
    }

    [Fact]
    public async Task RegisterAsync_EmailAlreadyExists_ThrowsApplicationException()
    {
        // Arrange
        var registerRequest = new RegisterRequest { Email = "existing@example.com", Password = "Password123!" };
        var existingUser = new ApplicationUser { Id = Guid.NewGuid(), Email = registerRequest.Email };

        _mockUserManager.Setup(um => um.FindByEmailAsync(registerRequest.Email))
            .ReturnsAsync(existingUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ApplicationException>(() => _userService.RegisterAsync(registerRequest));
        Assert.Equal("Email is already registered.", exception.Message);
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserCreatedEvent>()), Times.Never);
    }

    [Fact]
    public async Task GetUserByIdAsync_UserExists_ReturnsUserResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var appUser = new ApplicationUser { Id = userId, Name = "Test", Email = "test@example.com" };
        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync(appUser);

        // Act
        var result = await _userService.GetUserByIdAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal(appUser.Name, result.Name);
    }

    [Fact]
    public async Task GetUserByIdAsync_UserDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync((ApplicationUser)null!);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _userService.GetUserByIdAsync(userId));
    }

    [Fact]
    public async Task UpdateUserAsync_ValidUpdate_PublishesUserUpdatedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest { Name = "Updated Name", PhoneNumber = "1234567890" };
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", Name = "Old Name", PhoneNumber = "0987654321", IsPhoneConfirmed = true };

        _mockHttpContextAccessor.Setup(h => h.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier))
            .Returns(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        
        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        UserUpdatedEvent? publishedEvent = null;
        _mockEventPublisher.Setup(ep => ep.PublishAsync(It.IsAny<UserUpdatedEvent>()))
            .Callback<UserUpdatedEvent>(e => publishedEvent = e)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.UpdateUserAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.False(user.IsPhoneConfirmed); // Phone number changed
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserUpdatedEvent>()), Times.Once);
        Assert.NotNull(publishedEvent);
        Assert.Equal(userId, publishedEvent?.UserId);
        Assert.Equal(request.Name, publishedEvent?.Name);
        Assert.Equal(request.PhoneNumber, publishedEvent?.PhoneNumber);
        Assert.False(publishedEvent?.IsPhoneConfirmed);
    }

    [Fact]
    public async Task GeneratePhoneVerificationCodeAsync_ValidUser_PublishesEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var phoneNumber = "1112223333";
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", PhoneNumber = phoneNumber, Role = UserRole.Driver };
        var expectedToken = "test_token";

        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.GenerateChangePhoneNumberTokenAsync(user, phoneNumber)).ReturnsAsync(expectedToken);

        PhoneVerificationCodeGeneratedEvent? publishedEvent = null;
        _mockEventPublisher.Setup(ep => ep.PublishAsync(It.IsAny<PhoneVerificationCodeGeneratedEvent>()))
            .Callback<PhoneVerificationCodeGeneratedEvent>(e => publishedEvent = e)
            .Returns(Task.CompletedTask);
        
        // Act
        var token = await _userService.GeneratePhoneVerificationCodeAsync(userId);

        // Assert
        Assert.Equal(expectedToken, token);
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<PhoneVerificationCodeGeneratedEvent>()), Times.Once);
        Assert.NotNull(publishedEvent);
        Assert.Equal(userId, publishedEvent?.UserId);
        Assert.Equal(phoneNumber, publishedEvent?.PhoneNumber);
        Assert.Equal(expectedToken, publishedEvent?.VerificationCode);
        Assert.Equal((MessageContracts.Enums.UserRole)user.Role, publishedEvent?.Role);
    }

    [Fact]
    public async Task VerifyPhoneAsync_ValidCode_UpdatesUserAndPublishesEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "123456";
        var phoneNumber = "1234567890";
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", PhoneNumber = phoneNumber, IsPhoneConfirmed = false, Role = UserRole.Shipper };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.VerifyChangePhoneNumberTokenAsync(user, code, phoneNumber)).ReturnsAsync(true);
        _mockUserManager.Setup(um => um.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        UserVerifiedEvent? publishedEvent = null;
        _mockEventPublisher.Setup(ep => ep.PublishAsync(It.IsAny<UserVerifiedEvent>()))
            .Callback<UserVerifiedEvent>(e => publishedEvent = e)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.VerifyPhoneAsync(userId, code);

        // Assert
        Assert.True(result);
        Assert.True(user.IsPhoneConfirmed);
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserVerifiedEvent>()), Times.Once);
        Assert.NotNull(publishedEvent);
        Assert.Equal(userId, publishedEvent?.UserId);
        Assert.True(publishedEvent?.IsPhoneConfirmed);
        Assert.Equal(user.IsEmailConfirmed, publishedEvent?.IsEmailConfirmed);
    }

    [Fact]
    public async Task VerifyPhoneAsync_InvalidCode_ReturnsFalseAndDoesNotPublishEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "wrongcode";
        var phoneNumber = "1234567890";
        var user = new ApplicationUser { Id = userId, PhoneNumber = phoneNumber, IsPhoneConfirmed = false };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.VerifyChangePhoneNumberTokenAsync(user, code, phoneNumber)).ReturnsAsync(false);

        // Act
        var result = await _userService.VerifyPhoneAsync(userId, code);

        // Assert
        Assert.False(result);
        Assert.False(user.IsPhoneConfirmed);
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserVerifiedEvent>()), Times.Never);
    }
    
    [Fact]
    public async Task UpdateUserStatusAsync_StatusChanges_PublishesUserStatusChangedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var newStatus = UserStatus.Active;
        var oldStatus = UserStatus.PendingVerification;
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", Role = UserRole.Driver, Status = oldStatus };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        UserStatusChangedEvent? publishedEvent = null;
        _mockEventPublisher.Setup(ep => ep.PublishAsync(It.IsAny<UserStatusChangedEvent>()))
            .Callback<UserStatusChangedEvent>(e => publishedEvent = e)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.UpdateUserStatusAsync(userId, newStatus);

        // Assert
        Assert.True(result);
        Assert.Equal(newStatus, user.Status);
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserStatusChangedEvent>()), Times.Once);
        Assert.NotNull(publishedEvent);
        Assert.Equal(userId, publishedEvent?.UserId);
        Assert.Equal((MessageContracts.Enums.UserStatus)oldStatus, publishedEvent?.PreviousStatus);
        Assert.Equal((MessageContracts.Enums.UserStatus)newStatus, publishedEvent?.NewStatus);
        Assert.Equal(user.Email, publishedEvent?.Email);
        Assert.Equal((MessageContracts.Enums.UserRole)user.Role, publishedEvent?.Role);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_StatusSame_ReturnsTrueAndDoesNotPublishEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentStatus = UserStatus.Active;
        var user = new ApplicationUser { Id = userId, Status = currentStatus };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId.ToString())).ReturnsAsync(user);

        // Act
        var result = await _userService.UpdateUserStatusAsync(userId, currentStatus);

        // Assert
        Assert.True(result);
        _mockUserManager.Verify(um => um.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        _mockEventPublisher.Verify(ep => ep.PublishAsync(It.IsAny<UserStatusChangedEvent>()), Times.Never);
    }
}
