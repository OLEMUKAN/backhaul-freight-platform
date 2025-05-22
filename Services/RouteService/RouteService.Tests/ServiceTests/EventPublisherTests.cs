using Xunit;
using Moq;
using MassTransit;
using Microsoft.Extensions.Logging;
using RouteService.API.Services;
using RouteService.API.Services.Interfaces;
using MessageContracts.Events.Route;
using MessageContracts.Enums;
using System;
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

            _mockBus.Setup(b => b.Publish(It.IsAny<RouteCreatedEvent>(), default))
                .Callback<RouteCreatedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _eventPublisher.PublishRouteCreatedEventAsync(routeId);

            // Assert
            _mockBus.Verify(b => b.Publish(It.IsAny<RouteCreatedEvent>(), default), Times.Once);
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

            _mockBus.Setup(b => b.Publish(It.IsAny<RouteUpdatedEvent>(), default))
                .Callback<RouteUpdatedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _eventPublisher.PublishRouteUpdatedEventAsync(routeId);

            // Assert
            _mockBus.Verify(b => b.Publish(It.IsAny<RouteUpdatedEvent>(), default), Times.Once);
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

            _mockBus.Setup(b => b.Publish(It.IsAny<RouteStatusUpdatedEvent>(), default))
                .Callback<RouteStatusUpdatedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _eventPublisher.PublishRouteStatusUpdatedEventAsync(routeId, previousStatus, newStatus);

            // Assert
            _mockBus.Verify(b => b.Publish(It.IsAny<RouteStatusUpdatedEvent>(), default), Times.Once);
            Assert.NotNull(publishedEvent);
            Assert.Equal(routeId, publishedEvent.RouteId);
            Assert.Equal(previousStatus, publishedEvent.OldStatus);
            Assert.Equal(newStatus, publishedEvent.NewStatus);
            Assert.True((DateTime.UtcNow - publishedEvent.Timestamp).TotalSeconds < 5);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Published RouteStatusUpdatedEvent for RouteId: {routeId}. OldStatus: {previousStatus}, NewStatus: {newStatus}")),
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

            _mockBus.Setup(b => b.Publish(It.IsAny<RouteCapacityChangedEvent>(), default))
                .Callback<RouteCapacityChangedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _eventPublisher.PublishRouteCapacityChangedEventAsync(routeId, prevKg, newKg, prevM3, newM3);

            // Assert
            _mockBus.Verify(b => b.Publish(It.IsAny<RouteCapacityChangedEvent>(), default), Times.Once);
            Assert.NotNull(publishedEvent);
            Assert.Equal(routeId, publishedEvent.RouteId);
            Assert.Equal(prevKg, publishedEvent.PreviousAvailableCapacityKg);
            Assert.Equal(newKg, publishedEvent.NewAvailableCapacityKg);
            Assert.Equal(prevM3, publishedEvent.PreviousAvailableCapacityM3);
            Assert.Equal(newM3, publishedEvent.NewAvailableCapacityM3);
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

            _mockBus.Setup(b => b.Publish(It.IsAny<RouteCapacityChangedEvent>(), default))
                .Callback<RouteCapacityChangedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
                .Returns(Task.CompletedTask);

            // Act
            await _eventPublisher.PublishRouteCapacityChangedEventAsync(routeId, prevKg, newKg, prevM3, newM3);

            // Assert
            _mockBus.Verify(b => b.Publish(It.IsAny<RouteCapacityChangedEvent>(), default), Times.Once);
            Assert.NotNull(publishedEvent);
            Assert.Equal(routeId, publishedEvent.RouteId);
            Assert.Equal(prevKg, publishedEvent.PreviousAvailableCapacityKg);
            Assert.Equal(newKg, publishedEvent.NewAvailableCapacityKg);
            Assert.Null(publishedEvent.PreviousAvailableCapacityM3);
            Assert.Null(publishedEvent.NewAvailableCapacityM3);
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
