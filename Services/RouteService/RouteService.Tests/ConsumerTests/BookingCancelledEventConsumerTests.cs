using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Logging;
using Moq;
// using RouteService.API.Dtos.Routes;
using RouteService.API.Services.Interfaces;
using MessageContracts.Events.Booking;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore; // For InMemory DbContext
using RouteService.API.Data; // For RouteDbContext
using RouteService.API.Models; // For ProcessedEvent
using RouteService.API.Models.DTOs;
using RouteService.API.Consumers;

namespace RouteService.Tests.ConsumerTests
{
    public class BookingCancelledEventConsumerTests : IDisposable
    {
        private readonly Mock<IRouteService> _mockRouteService;
        private readonly Mock<ILogger<BookingCancelledEventConsumer>> _mockLogger;
        private readonly RouteDbContext _dbContext;
        private readonly BookingCancelledEventConsumer _consumer;

        public BookingCancelledEventConsumerTests()
        {
            _mockRouteService = new Mock<IRouteService>();
            _mockLogger = new Mock<ILogger<BookingCancelledEventConsumer>>();

            var options = new DbContextOptionsBuilder<RouteDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB for each test run
                .Options;
            _dbContext = new RouteDbContext(options);

            _consumer = new BookingCancelledEventConsumer(_mockRouteService.Object, _mockLogger.Object, _dbContext);
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task Consume_ValidEvent_CallsUpdateRouteCapacityWithPositiveValues()
        {
            // Arrange
            var routeId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var bookedWeightKg = 100m;
            var bookedVolumeM3 = 10m;

            var bookingCancelledEvent = new BookingCancelledEvent
            {
                BookingId = bookingId,
                RouteId = routeId,
                BookedWeightKg = bookedWeightKg,
                BookedVolumeM3 = bookedVolumeM3,
                Timestamp = DateTime.UtcNow
            };

            _mockRouteService.Setup(s => s.UpdateRouteCapacityAsync(routeId, It.IsAny<UpdateRouteCapacityRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RouteDto { Id = routeId }); // Return a dummy DTO

            var consumeContextMock = new Mock<ConsumeContext<BookingCancelledEvent>>();
            consumeContextMock.Setup(c => c.Message).Returns(bookingCancelledEvent);

            // Act
            await _consumer.Consume(consumeContextMock.Object);

            // Assert
            _mockRouteService.Verify(s => s.UpdateRouteCapacityAsync(
                routeId,
                It.Is<UpdateRouteCapacityRequest>(req =>
                    req.BookingId == bookingId &&
                    req.CapacityChangeKg == bookedWeightKg && // Positive value
                    req.CapacityChangeM3 == bookedVolumeM3 && // Positive value
                    req.Reason.Contains(bookingId.ToString())
                ), It.IsAny<CancellationToken>()), Times.Once);

            // Verify ProcessedEvent was created
            var processedEvent = await _dbContext.ProcessedEvents.FindAsync(bookingId);
            Assert.NotNull(processedEvent);
            Assert.Equal(bookingId, processedEvent.EventId);
            Assert.True((DateTimeOffset.UtcNow - processedEvent.ProcessedAt).TotalSeconds < 5);

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
        public async Task Consume_DuplicateEvent_SkipsProcessingAndLogs()
        {
            // Arrange
            var routeId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var bookingCancelledEvent = new BookingCancelledEvent
            {
                BookingId = bookingId,
                RouteId = routeId,
                BookedWeightKg = 100m,
                Timestamp = DateTime.UtcNow
            };

            // Add a ProcessedEvent entry to simulate it being already processed
            _dbContext.ProcessedEvents.Add(new ProcessedEvent { EventId = bookingId, ProcessedAt = DateTimeOffset.UtcNow.AddMinutes(-5) });
            await _dbContext.SaveChangesAsync();

            var consumeContextMock = new Mock<ConsumeContext<BookingCancelledEvent>>();
            consumeContextMock.Setup(c => c.Message).Returns(bookingCancelledEvent);
            consumeContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

            // Act
            await _consumer.Consume(consumeContextMock.Object);

            // Assert
            _mockRouteService.Verify(s => s.UpdateRouteCapacityAsync(It.IsAny<Guid>(), It.IsAny<UpdateRouteCapacityRequest>(), It.IsAny<CancellationToken>()), Times.Never());

            var processedEventCount = await _dbContext.ProcessedEvents.CountAsync(pe => pe.EventId == bookingId);
            Assert.Equal(1, processedEventCount); // Ensure no new entry was added

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"BookingCancelledEvent for BookingId: {bookingId} already processed. Skipping.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_RouteServiceReturnsNull_LogsWarningAndMarksAsProcessed()
        {
            // Arrange
            var bookingCancelledEvent = new BookingCancelledEvent
            {
                BookingId = Guid.NewGuid(),
                RouteId = Guid.NewGuid(),
                BookedWeightKg = 100m,
                Timestamp = DateTime.UtcNow
            };

            _mockRouteService.Setup(s => s.UpdateRouteCapacityAsync(bookingCancelledEvent.RouteId, It.IsAny<UpdateRouteCapacityRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((RouteDto)null); // Simulate route not found or update failure

            var consumeContextMock = new Mock<ConsumeContext<BookingCancelledEvent>>();
            consumeContextMock.Setup(c => c.Message).Returns(bookingCancelledEvent);

            // Act
            await _consumer.Consume(consumeContextMock.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Route {bookingCancelledEvent.RouteId} not found or update failed")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            // Verify ProcessedEvent was created even if route update failed
            var processedEvent = await _dbContext.ProcessedEvents.FindAsync(bookingCancelledEvent.BookingId);
            Assert.NotNull(processedEvent);
        }

        [Fact]
        public async Task Consume_RouteServiceThrowsArgumentException_LogsWarningAndDoesNotThrowAndDoesNotMarkProcessed()
        {
            // Arrange
            var bookingId = Guid.NewGuid();
            var bookingCancelledEvent = new BookingCancelledEvent
            {
                BookingId = bookingId,
                RouteId = Guid.NewGuid(),
                BookedWeightKg = 100m,
                Timestamp = DateTime.UtcNow
            };

            _mockRouteService.Setup(s => s.UpdateRouteCapacityAsync(bookingCancelledEvent.RouteId, It.IsAny<UpdateRouteCapacityRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Test ArgumentException"));

            var consumeContextMock = new Mock<ConsumeContext<BookingCancelledEvent>>();
            consumeContextMock.Setup(c => c.Message).Returns(bookingCancelledEvent);

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
            var bookingCancelledEvent = new BookingCancelledEvent
            {
                BookingId = Guid.NewGuid(),
                RouteId = Guid.NewGuid(),
                BookedWeightKg = 100m,
                Timestamp = DateTime.UtcNow
            };
            var expectedException = new InvalidOperationException("Test generic exception");

            _mockRouteService.Setup(s => s.UpdateRouteCapacityAsync(bookingCancelledEvent.RouteId, It.IsAny<UpdateRouteCapacityRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            var consumeContextMock = new Mock<ConsumeContext<BookingCancelledEvent>>();
            consumeContextMock.Setup(c => c.Message).Returns(bookingCancelledEvent);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _consumer.Consume(consumeContextMock.Object));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("An unexpected error occurred while processing BookingCancelledEvent")),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
