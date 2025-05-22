using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using RouteService.API.Consumers;
using RouteService.API.Dtos.Routes;
using RouteService.API.Services.Interfaces;
using MessageContracts.Events.Booking;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RouteService.Tests.ConsumerTests
{
    public class BookingConfirmedEventConsumerTests
    {
        private readonly Mock<IRouteService> _mockRouteService;
        private readonly Mock<ILogger<BookingConfirmedEventConsumer>> _mockLogger;
        private readonly BookingConfirmedEventConsumer _consumer;

        public BookingConfirmedEventConsumerTests()
        {
            _mockRouteService = new Mock<IRouteService>();
            _mockLogger = new Mock<ILogger<BookingConfirmedEventConsumer>>();
            _consumer = new BookingConfirmedEventConsumer(_mockRouteService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Consume_ValidEvent_CallsUpdateRouteCapacityWithNegativeValues()
        {
            // Arrange
            var routeId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var bookedWeightKg = 100m;
            var bookedVolumeM3 = 10m;

            var bookingConfirmedEvent = new BookingConfirmedEvent
            {
                BookingId = bookingId,
                RouteId = routeId,
                BookedWeightKg = bookedWeightKg,
                BookedVolumeM3 = bookedVolumeM3,
                Timestamp = DateTime.UtcNow
            };

            _mockRouteService.Setup(s => s.UpdateRouteCapacityAsync(routeId, It.IsAny<UpdateRouteCapacityRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RouteDto { Id = routeId }); // Return a dummy DTO

            var consumeContextMock = new Mock<ConsumeContext<BookingConfirmedEvent>>();
            consumeContextMock.Setup(c => c.Message).Returns(bookingConfirmedEvent);

            // Act
            await _consumer.Consume(consumeContextMock.Object);

            // Assert
            _mockRouteService.Verify(s => s.UpdateRouteCapacityAsync(
                routeId,
                It.Is<UpdateRouteCapacityRequest>(req =>
                    req.BookingId == bookingId &&
                    req.CapacityChangeKg == -bookedWeightKg &&
                    req.CapacityChangeM3 == -bookedVolumeM3 &&
                    req.Reason.Contains(bookingId.ToString())
                ), It.IsAny<CancellationToken>()), Times.Once);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Successfully updated capacity for RouteId: {routeId}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_RouteServiceReturnsNull_LogsWarning()
        {
            // Arrange
            var bookingConfirmedEvent = new BookingConfirmedEvent
            {
                BookingId = Guid.NewGuid(),
                RouteId = Guid.NewGuid(),
                BookedWeightKg = 100m,
                Timestamp = DateTime.UtcNow
            };

            _mockRouteService.Setup(s => s.UpdateRouteCapacityAsync(bookingConfirmedEvent.RouteId, It.IsAny<UpdateRouteCapacityRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((RouteDto)null); // Simulate route not found or update failure

            var consumeContextMock = new Mock<ConsumeContext<BookingConfirmedEvent>>();
            consumeContextMock.Setup(c => c.Message).Returns(bookingConfirmedEvent);

            // Act
            await _consumer.Consume(consumeContextMock.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Route {bookingConfirmedEvent.RouteId} not found or update failed")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_RouteServiceThrowsArgumentException_LogsWarningAndDoesNotThrow()
        {
            // Arrange
            var bookingConfirmedEvent = new BookingConfirmedEvent
            {
                BookingId = Guid.NewGuid(),
                RouteId = Guid.NewGuid(),
                BookedWeightKg = 100m,
                Timestamp = DateTime.UtcNow
            };

            _mockRouteService.Setup(s => s.UpdateRouteCapacityAsync(bookingConfirmedEvent.RouteId, It.IsAny<UpdateRouteCapacityRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Test ArgumentException"));

            var consumeContextMock = new Mock<ConsumeContext<BookingConfirmedEvent>>();
            consumeContextMock.Setup(c => c.Message).Returns(bookingConfirmedEvent);

            // Act
            await _consumer.Consume(consumeContextMock.Object); // Should not throw

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("ArgumentException while updating capacity")),
                    It.IsAny<ArgumentException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        
        [Fact]
        public async Task Consume_RouteServiceThrowsGenericException_LogsErrorAndThrows()
        {
            // Arrange
            var bookingConfirmedEvent = new BookingConfirmedEvent
            {
                BookingId = Guid.NewGuid(),
                RouteId = Guid.NewGuid(),
                BookedWeightKg = 100m,
                Timestamp = DateTime.UtcNow
            };
            var expectedException = new InvalidOperationException("Test generic exception");

            _mockRouteService.Setup(s => s.UpdateRouteCapacityAsync(bookingConfirmedEvent.RouteId, It.IsAny<UpdateRouteCapacityRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            var consumeContextMock = new Mock<ConsumeContext<BookingConfirmedEvent>>();
            consumeContextMock.Setup(c => c.Message).Returns(bookingConfirmedEvent);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _consumer.Consume(consumeContextMock.Object));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("An unexpected error occurred while processing BookingConfirmedEvent")),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
