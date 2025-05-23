using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using MassTransit; // Required for ConsumeContext
using MessageContracts.Events.Truck;
using MessageContracts.Events.Booking;
using UserService.API.Events;
using UserService.API.Models;

// MockUserStore is defined in UserServiceTests.cs, ensure it's accessible or redefine/share it.
// For simplicity, if these tests are in the same assembly, it might be accessible.
// If not, you'd need to define it here or in a shared test utilities project.

public class UserEventConsumersTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    
    // TruckVerifiedConsumer Mocks
    private readonly Mock<ILogger<TruckVerifiedConsumer>> _mockTruckVerifiedLogger;
    private readonly TruckVerifiedConsumer _truckVerifiedConsumer;

    // BookingCompletedConsumer Mocks
    private readonly Mock<ILogger<BookingCompletedConsumer>> _mockBookingCompletedLogger;
    private readonly BookingCompletedConsumer _bookingCompletedConsumer;

    public UserEventConsumersTests()
    {
        // Common UserManager mock
        var userStore = new MockUserStore(); // Assumes MockUserStore is accessible
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        // Setup for TruckVerifiedConsumer
        _mockTruckVerifiedLogger = new Mock<ILogger<TruckVerifiedConsumer>>();
        _truckVerifiedConsumer = new TruckVerifiedConsumer(
            _mockUserManager.Object,
            _mockTruckVerifiedLogger.Object
        );

        // Setup for BookingCompletedConsumer
        _mockBookingCompletedLogger = new Mock<ILogger<BookingCompletedConsumer>>();
        _bookingCompletedConsumer = new BookingCompletedConsumer(
            _mockUserManager.Object,
            _mockBookingCompletedLogger.Object
        );
    }

    // --- TruckVerifiedConsumer Tests ---

    [Fact]
    public async Task TruckVerifiedConsumer_Consume_WhenIsVerifiedTrue_SetsHasVerifiedTruckTrue()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var truckId = Guid.NewGuid();
        var truckVerifiedEvent = new TruckVerifiedEvent
        {
            OwnerId = ownerId,
            TruckId = truckId,
            IsVerified = true,
            VerifiedAt = DateTimeOffset.UtcNow
        };
        var mockConsumeContext = new Mock<ConsumeContext<TruckVerifiedEvent>>();
        mockConsumeContext.Setup(ctx => ctx.Message).Returns(truckVerifiedEvent);

        var user = new ApplicationUser { Id = ownerId, HasVerifiedTruck = false };
        _mockUserManager.Setup(um => um.FindByIdAsync(ownerId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _truckVerifiedConsumer.Consume(mockConsumeContext.Object);

        // Assert
        Assert.True(user.HasVerifiedTruck);
        _mockUserManager.Verify(um => um.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task TruckVerifiedConsumer_Consume_WhenIsVerifiedFalse_SetsHasVerifiedTruckFalse()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var truckId = Guid.NewGuid();
        var truckVerifiedEvent = new TruckVerifiedEvent
        {
            OwnerId = ownerId,
            TruckId = truckId,
            IsVerified = false, // Key difference
            VerifiedAt = DateTimeOffset.UtcNow
        };
        var mockConsumeContext = new Mock<ConsumeContext<TruckVerifiedEvent>>();
        mockConsumeContext.Setup(ctx => ctx.Message).Returns(truckVerifiedEvent);

        var user = new ApplicationUser { Id = ownerId, HasVerifiedTruck = true }; // Start with true
        _mockUserManager.Setup(um => um.FindByIdAsync(ownerId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _truckVerifiedConsumer.Consume(mockConsumeContext.Object);

        // Assert
        Assert.False(user.HasVerifiedTruck);
        _mockUserManager.Verify(um => um.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task TruckVerifiedConsumer_Consume_UserNotFound_LogsWarningAndReturns()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var truckVerifiedEvent = new TruckVerifiedEvent { OwnerId = ownerId, IsVerified = true };
        var mockConsumeContext = new Mock<ConsumeContext<TruckVerifiedEvent>>();
        mockConsumeContext.Setup(ctx => ctx.Message).Returns(truckVerifiedEvent);

        _mockUserManager.Setup(um => um.FindByIdAsync(ownerId.ToString())).ReturnsAsync((ApplicationUser)null!);

        // Act
        await _truckVerifiedConsumer.Consume(mockConsumeContext.Object);

        // Assert
        _mockUserManager.Verify(um => um.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        // Optionally verify logger call: _mockTruckVerifiedLogger.Verify(...)
    }

    // --- BookingCompletedConsumer Tests ---

    [Fact]
    public async Task BookingCompletedConsumer_Consume_WithShipperRating_UpdatesTruckOwnerRating()
    {
        // Arrange
        var truckOwnerId = Guid.NewGuid();
        var shipperId = Guid.NewGuid();
        var bookingCompletedEvent = new BookingCompletedEvent
        {
            BookingId = Guid.NewGuid(),
            TruckOwnerId = truckOwnerId,
            ShipperId = shipperId,
            ShipperRatingGiven = 4 // Rating for Truck Owner
        };
        var mockConsumeContext = new Mock<ConsumeContext<BookingCompletedEvent>>();
        mockConsumeContext.Setup(ctx => ctx.Message).Returns(bookingCompletedEvent);

        var truckOwner = new ApplicationUser { Id = truckOwnerId, Rating = 3.0, NumberOfRatings = 1 };
        _mockUserManager.Setup(um => um.FindByIdAsync(truckOwnerId.ToString())).ReturnsAsync(truckOwner);
        _mockUserManager.Setup(um => um.UpdateAsync(truckOwner)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _bookingCompletedConsumer.Consume(mockConsumeContext.Object);

        // Assert
        Assert.Equal(2, truckOwner.NumberOfRatings); // 1 (old) + 1 (new) = 2
        Assert.Equal(3.5, truckOwner.Rating); // (3.0 * 1 + 4) / 2 = 3.5
        _mockUserManager.Verify(um => um.UpdateAsync(truckOwner), Times.Once);
    }

    [Fact]
    public async Task BookingCompletedConsumer_Consume_WithTruckOwnerRating_UpdatesShipperRating()
    {
        // Arrange
        var truckOwnerId = Guid.NewGuid();
        var shipperId = Guid.NewGuid();
        var bookingCompletedEvent = new BookingCompletedEvent
        {
            BookingId = Guid.NewGuid(),
            TruckOwnerId = truckOwnerId,
            ShipperId = shipperId,
            TruckOwnerRatingGiven = 5 // Rating for Shipper
        };
        var mockConsumeContext = new Mock<ConsumeContext<BookingCompletedEvent>>();
        mockConsumeContext.Setup(ctx => ctx.Message).Returns(bookingCompletedEvent);

        var shipper = new ApplicationUser { Id = shipperId, Rating = null, NumberOfRatings = 0 }; // No prior ratings
        _mockUserManager.Setup(um => um.FindByIdAsync(shipperId.ToString())).ReturnsAsync(shipper);
        _mockUserManager.Setup(um => um.UpdateAsync(shipper)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _bookingCompletedConsumer.Consume(mockConsumeContext.Object);

        // Assert
        Assert.Equal(1, shipper.NumberOfRatings);
        Assert.Equal(5.0, shipper.Rating); // (0 * 0 + 5) / 1 = 5.0
        _mockUserManager.Verify(um => um.UpdateAsync(shipper), Times.Once);
    }
    
    [Fact]
    public async Task BookingCompletedConsumer_Consume_WithBothRatings_UpdatesBothUsers()
    {
        // Arrange
        var truckOwnerId = Guid.NewGuid();
        var shipperId = Guid.NewGuid();
        var bookingCompletedEvent = new BookingCompletedEvent
        {
            BookingId = Guid.NewGuid(),
            TruckOwnerId = truckOwnerId,
            ShipperId = shipperId,
            ShipperRatingGiven = 4, // For Truck Owner
            TruckOwnerRatingGiven = 3  // For Shipper
        };
        var mockConsumeContext = new Mock<ConsumeContext<BookingCompletedEvent>>();
        mockConsumeContext.Setup(ctx => ctx.Message).Returns(bookingCompletedEvent);

        var truckOwner = new ApplicationUser { Id = truckOwnerId, Rating = 5.0, NumberOfRatings = 1 };
        var shipper = new ApplicationUser { Id = shipperId, Rating = 2.0, NumberOfRatings = 1 };

        _mockUserManager.Setup(um => um.FindByIdAsync(truckOwnerId.ToString())).ReturnsAsync(truckOwner);
        _mockUserManager.Setup(um => um.FindByIdAsync(shipperId.ToString())).ReturnsAsync(shipper);
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        // Act
        await _bookingCompletedConsumer.Consume(mockConsumeContext.Object);

        // Assert (Truck Owner)
        Assert.Equal(2, truckOwner.NumberOfRatings);
        Assert.Equal(4.5, truckOwner.Rating); // (5.0 * 1 + 4) / 2 = 4.5
        
        // Assert (Shipper)
        Assert.Equal(2, shipper.NumberOfRatings);
        Assert.Equal(2.5, shipper.Rating); // (2.0 * 1 + 3) / 2 = 2.5

        _mockUserManager.Verify(um => um.UpdateAsync(truckOwner), Times.Once);
        _mockUserManager.Verify(um => um.UpdateAsync(shipper), Times.Once);
    }

    [Fact]
    public async Task BookingCompletedConsumer_Consume_InvalidRatingValue_DoesNotUpdateRating()
    {
        // Arrange
        var truckOwnerId = Guid.NewGuid();
        var bookingCompletedEvent = new BookingCompletedEvent
        {
            TruckOwnerId = truckOwnerId,
            ShipperRatingGiven = 0 // Invalid rating (should be 1-5)
        };
        var mockConsumeContext = new Mock<ConsumeContext<BookingCompletedEvent>>();
        mockConsumeContext.Setup(ctx => ctx.Message).Returns(bookingCompletedEvent);

        var truckOwner = new ApplicationUser { Id = truckOwnerId, Rating = 3.0, NumberOfRatings = 1 };
        _mockUserManager.Setup(um => um.FindByIdAsync(truckOwnerId.ToString())).ReturnsAsync(truckOwner);

        // Act
        await _bookingCompletedConsumer.Consume(mockConsumeContext.Object);

        // Assert
        Assert.Equal(1, truckOwner.NumberOfRatings); // Should not change
        Assert.Equal(3.0, truckOwner.Rating);      // Should not change
        _mockUserManager.Verify(um => um.UpdateAsync(truckOwner), Times.Never);
    }
    
    [Fact]
    public async Task BookingCompletedConsumer_Consume_TruckOwnerNotFound_DoesNotUpdateAndLogs()
    {
        // Arrange
        var truckOwnerId = Guid.NewGuid();
        var bookingCompletedEvent = new BookingCompletedEvent
        {
            TruckOwnerId = truckOwnerId,
            ShipperRatingGiven = 5 
        };
        var mockConsumeContext = new Mock<ConsumeContext<BookingCompletedEvent>>();
        mockConsumeContext.Setup(ctx => ctx.Message).Returns(bookingCompletedEvent);

        _mockUserManager.Setup(um => um.FindByIdAsync(truckOwnerId.ToString())).ReturnsAsync((ApplicationUser)null!);

        // Act
        await _bookingCompletedConsumer.Consume(mockConsumeContext.Object);

        // Assert
        _mockUserManager.Verify(um => um.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        // Optionally verify logger: _mockBookingCompletedLogger.Verify(log => log.LogWarning(...), Times.Once);
    }
}
