using Xunit;
using Moq;
using MassTransit;
using Microsoft.Extensions.Logging;
using RouteService.API.Services;
using RouteService.API.Services.Interfaces;
using MessageContracts.Events.Route;
using MessageContracts.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RouteService.Tests.ServiceTests
{
    public class EventPublisherTests
    {
        private readonly Mock<IBus> _mockBus;
        private readonly Mock<ILogger<EventPublisher>> _mockLogger;
        private readonly IEventPublisher _eventPublisher;

        public EventPublisherTests()
        {
            _mockBus = new Mock<IBus>();
            _mockLogger = new Mock<ILogger<EventPublisher>>();
            _eventPublisher = new EventPublisher(_mockBus.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task PublishRouteCreatedEventAsync_PublishesCorrectEventAndLogs()
        {
            // Arrange
            var routeId = Guid.NewGuid();
            RouteCreatedEvent publishedEvent = null;

            _mockBus.Setup(b => b.Publish(It.IsAny<RouteCreatedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<RouteCreatedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _eventPublisher.PublishRouteCreatedEventAsync(routeId, CancellationToken.None);

            // Assert
            _mockBus.Verify(b => b.Publish(It.IsAny<RouteCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(publishedEvent);
            Assert.Equal(routeId, publishedEvent.RouteId);
            Assert.True((DateTime.UtcNow - publishedEvent.Timestamp).TotalSeconds < 5); // Check if timestamp is recent

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Published RouteCreatedEvent for RouteId: {routeId}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task PublishRouteUpdatedEventAsync_PublishesCorrectEventAndLogs()
        {
            // Arrange
            var routeId = Guid.NewGuid();
            RouteUpdatedEvent publishedEvent = null;

            _mockBus.Setup(b => b.Publish(It.IsAny<RouteUpdatedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<RouteUpdatedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _eventPublisher.PublishRouteUpdatedEventAsync(routeId, CancellationToken.None);

            // Assert
            _mockBus.Verify(b => b.Publish(It.IsAny<RouteUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(publishedEvent);
            Assert.Equal(routeId, publishedEvent.RouteId);
            Assert.True((DateTime.UtcNow - publishedEvent.Timestamp).TotalSeconds < 5);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Published RouteUpdatedEvent for RouteId: {routeId}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task PublishRouteStatusUpdatedEventAsync_PublishesCorrectEventAndLogs()
        {
            // Arrange
            var routeId = Guid.NewGuid();
            var previousStatus = RouteStatus.Planned;
            var newStatus = RouteStatus.InProgress;
            RouteStatusUpdatedEvent publishedEvent = null;

            _mockBus.Setup(b => b.Publish(It.IsAny<RouteStatusUpdatedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<RouteStatusUpdatedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _eventPublisher.PublishRouteStatusUpdatedEventAsync(routeId, previousStatus, newStatus, CancellationToken.None);

            // Assert
            _mockBus.Verify(b => b.Publish(It.IsAny<RouteStatusUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(publishedEvent);
            Assert.Equal(routeId, publishedEvent.RouteId);
            Assert.Equal((int)previousStatus, publishedEvent.PreviousStatus); // Changed property and cast
            Assert.Equal((int)newStatus, publishedEvent.NewStatus);         // Cast
            Assert.True((DateTime.UtcNow - publishedEvent.Timestamp).TotalSeconds < 5);
            // OwnerId and StatusChangeReason are not asserted as they are omitted in the event for now.

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Published RouteStatusUpdatedEvent for RouteId: {routeId}. PreviousStatus: {previousStatus}, NewStatus: {newStatus}")), // Log message updated
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task PublishRouteCapacityChangedEventAsync_PublishesCorrectEventAndLogs()
        {
            // Arrange
            var routeId = Guid.NewGuid();
            var prevKg = 1000m;
            var newKg = 800m;
            decimal? prevM3 = 50m;
            decimal? newM3 = 40m;
            RouteCapacityChangedEvent publishedEvent = null;

            _mockBus.Setup(b => b.Publish(It.IsAny<RouteCapacityChangedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<RouteCapacityChangedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _eventPublisher.PublishRouteCapacityChangedEventAsync(routeId, prevKg, newKg, prevM3, newM3, CancellationToken.None);

            // Assert
            _mockBus.Verify(b => b.Publish(It.IsAny<RouteCapacityChangedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(publishedEvent);
            Assert.Equal(routeId, publishedEvent.RouteId);
            Assert.Equal(prevKg, publishedEvent.PreviousCapacityAvailableKg);
            Assert.Equal(newKg, publishedEvent.NewCapacityAvailableKg);
            Assert.Equal(prevM3, publishedEvent.PreviousCapacityAvailableM3);
            Assert.Equal(newM3, publishedEvent.NewCapacityAvailableM3);
            Assert.True((DateTime.UtcNow - publishedEvent.Timestamp).TotalSeconds < 5);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Published RouteCapacityChangedEvent for RouteId: {routeId}. PrevKg: {prevKg}, NewKg: {newKg}, PrevM3: {prevM3}, NewM3: {newM3}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        
        [Fact]
        public async Task PublishRouteCapacityChangedEventAsync_WithNullM3_PublishesCorrectEventAndLogs()
        {
            // Arrange
            var routeId = Guid.NewGuid();
            var prevKg = 1000m;
            var newKg = 800m;
            decimal? prevM3 = null; // Test case with null M3
            decimal? newM3 = null;   // Test case with null M3
            RouteCapacityChangedEvent publishedEvent = null;

            _mockBus.Setup(b => b.Publish(It.IsAny<RouteCapacityChangedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<RouteCapacityChangedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _eventPublisher.PublishRouteCapacityChangedEventAsync(routeId, prevKg, newKg, prevM3, newM3, CancellationToken.None);

            // Assert
            _mockBus.Verify(b => b.Publish(It.IsAny<RouteCapacityChangedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(publishedEvent);
            Assert.Equal(routeId, publishedEvent.RouteId);
            Assert.Equal(prevKg, publishedEvent.PreviousCapacityAvailableKg);
            Assert.Equal(newKg, publishedEvent.NewCapacityAvailableKg);
            Assert.Null(publishedEvent.PreviousCapacityAvailableM3);
            Assert.Null(publishedEvent.NewCapacityAvailableM3);
            Assert.True((DateTime.UtcNow - publishedEvent.Timestamp).TotalSeconds < 5);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Published RouteCapacityChangedEvent for RouteId: {routeId}. PrevKg: {prevKg}, NewKg: {newKg}, PrevM3: {prevM3}, NewM3: {newM3}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
