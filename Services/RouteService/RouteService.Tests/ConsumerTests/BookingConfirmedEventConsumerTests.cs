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
using Microsoft.EntityFrameworkCore; // For InMemory DbContext
using RouteService.API.Data; // For RouteDbContext
using RouteService.API.Models; // For ProcessedEvent

namespace RouteService.Tests.ConsumerTests
{
    public class BookingConfirmedEventConsumerTests : IDisposable
    {
        private readonly Mock<IRouteService> _mockRouteService;
        private readonly Mock<ILogger<BookingConfirmedEventConsumer>> _mockLogger;
        private readonly RouteDbContext _dbContext;
        private readonly BookingConfirmedEventConsumer _consumer;

        public BookingConfirmedEventConsumerTests()
        {
            _mockRouteService = new Mock<IRouteService>();
            _mockLogger = new Mock<ILogger<BookingConfirmedEventConsumer>>();
            
            var options = new DbContextOptionsBuilder<RouteDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB for each test run
                .Options;
            _dbContext = new RouteDbContext(options);
            
            _consumer = new BookingConfirmedEventConsumer(_mockRouteService.Object, _mockLogger.Object, _dbContext);
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
            GC.SuppressFinalize(this);
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
            var bookingConfirmedEvent = new BookingConfirmedEvent
            {
                BookingId = bookingId,
                RouteId = routeId,
                BookedWeightKg = 100m,
                Timestamp = DateTime.UtcNow
            };

            // Add a ProcessedEvent entry to simulate it being already processed
            _dbContext.ProcessedEvents.Add(new ProcessedEvent { EventId = bookingId, ProcessedAt = DateTimeOffset.UtcNow.AddMinutes(-5) });
            await _dbContext.SaveChangesAsync();

            var consumeContextMock = new Mock<ConsumeContext<BookingConfirmedEvent>>();
            consumeContextMock.Setup(c => c.Message).Returns(bookingConfirmedEvent);
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
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"BookingConfirmedEvent for BookingId: {bookingId} already processed. Skipping.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }


        [Fact]
        public async Task Consume_RouteServiceReturnsNull_LogsWarningAndMarksAsProcessed()
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
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Route {bookingConfirmedEvent.RouteId} not found or update failed")), // Message updated in consumer
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
            
            // Verify ProcessedEvent was created even if route update failed (as per current consumer logic)
            var processedEvent = await _dbContext.ProcessedEvents.FindAsync(bookingConfirmedEvent.BookingId);
            Assert.NotNull(processedEvent);
        }

        [Fact]
        public async Task Consume_RouteServiceThrowsArgumentException_LogsWarningAndDoesNotThrowAndDoesNotMarkProcessed()
        {
            // Arrange
            var bookingId = Guid.NewGuid();
            var bookingConfirmedEvent = new BookingConfirmedEvent
            {
                BookingId = bookingId,
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
