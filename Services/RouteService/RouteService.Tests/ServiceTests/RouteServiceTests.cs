using AutoMapper;
using MessageContracts.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;
using RouteService.API; // For MappingProfile
using RouteService.API.Data;
using RouteService.API.Dtos.Routes;
using RouteService.API.Models.Routes;
using RouteService.API.Services.Interfaces; // For IRouteService and other service interfaces
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json; // For JsonSerializer if needed directly in tests, though mapping should handle
using System.Threading;
using System.Threading.Tasks;
using Xunit;

// Fully qualify RouteService to avoid ambiguity with the namespace RouteService
using ApiRouteService = RouteService.API.Services.RouteService;

namespace RouteService.Tests.ServiceTests
{
    public class RouteServiceTests : IDisposable
    {
        private readonly RouteDbContext _dbContext; // Use real DbContext with InMemory provider
        private readonly Mock<IGeospatialService> _mockGeospatialService;
        private readonly Mock<ITruckServiceClient> _mockTruckServiceClient;
        private readonly Mock<IEventPublisher> _mockEventPublisher;
        private readonly IMapper _mapper;
        private readonly Mock<ILogger<ApiRouteService>> _mockLogger;
        private readonly ApiRouteService _routeService;

        // Sample points for consistent testing
        private readonly Point _sampleOriginPoint = new Point(10, 20) { SRID = 4326 };
        private readonly Point _sampleDestPoint = new Point(30, 40) { SRID = 4326 };
        private readonly Point _sampleViaPoint1 = new Point(15, 25) { SRID = 4326 };
        private readonly Point _sampleViaPoint2 = new Point(25, 35) { SRID = 4326 };


        public RouteServiceTests()
        {
            // Configure InMemory database
            var options = new DbContextOptionsBuilder<RouteDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique name for each test run
                .Options;
            _dbContext = new RouteDbContext(options);

            _mockGeospatialService = new Mock<IGeospatialService>();
            _mockTruckServiceClient = new Mock<ITruckServiceClient>();
            _mockEventPublisher = new Mock<IEventPublisher>();
            _mockLogger = new Mock<ILogger<ApiRouteService>>();

            var mappingConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new MappingProfile());
                // Provide IGeospatialService to AutoMapper context
                cfg.ConstructServicesUsing(type =>
                    type == typeof(IGeospatialService) ? _mockGeospatialService.Object :
                    // Add other services if MappingProfile directly resolves them, though it's better if it doesn't
                    null);
            });
            _mapper = mappingConfig.CreateMapper();

            _routeService = new ApiRouteService(
                _dbContext,
                _mockGeospatialService.Object,
                _mockTruckServiceClient.Object,
                _mockEventPublisher.Object,
                _mapper,
                _mockLogger.Object
            );

            // Default GeospatialService setups
            _mockGeospatialService.Setup(g => g.CreatePoint(It.IsAny<double>(), It.IsAny<double>()))
                .Returns((double lon, double lat) => new Point(lon, lat) { SRID = 4326 });
            _mockGeospatialService.Setup(g => g.ValidatePoint(It.IsAny<Point>())).Returns(true);
            _mockGeospatialService.Setup(g => g.PointToCoordinateArray(It.IsAny<Point>()))
                .Returns((Point p) => new[] { p.X, p.Y });
            _mockGeospatialService.Setup(g => g.CreateLineString(It.IsAny<IEnumerable<Point>>()))
                .Returns((IEnumerable<Point> points) => points.Any() ? new LineString(points.Select(p => p.Coordinate).ToArray()){ SRID = 4326 } : null);
            _mockGeospatialService.Setup(g => g.CalculateDistanceInKilometers(It.IsAny<Point>(), It.IsAny<Point>()))
                .Returns((Point p1, Point p2) => p1.Distance(p2) * 111); // Rough approximation for distance for testing
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted(); // Clean up InMemory database after each test
            GC.SuppressFinalize(this); // Recommended for IDisposable
            _dbContext.Dispose();
        }

        // --- CreateRouteAsync Tests ---
        [Fact]
        public async Task CreateRouteAsync_ValidRequest_ReturnsRouteDtoAndPublishesEvent()
        {
            var ownerId = Guid.NewGuid();
            var truckId = Guid.NewGuid();
            var request = new CreateRouteRequest
            {
                TruckId = truckId,
                OriginCoordinates = new[] { _sampleOriginPoint.X, _sampleOriginPoint.Y },
                DestinationCoordinates = new[] { _sampleDestPoint.X, _sampleDestPoint.Y },
                ScheduledDeparture = DateTimeOffset.UtcNow.AddHours(1),
                ScheduledArrival = DateTimeOffset.UtcNow.AddHours(5),
                IsReturnLeg = false
            };

            _mockTruckServiceClient.Setup(ts => ts.VerifyTruckOwnershipAsync(truckId, ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockTruckServiceClient.Setup(ts => ts.GetTruckCapacityAsync(truckId, It.IsAny<CancellationToken>())).ReturnsAsync((1000m, 50m));
            _mockEventPublisher.Setup(ep => ep.PublishRouteCreatedEventAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            
            _mockGeospatialService.Setup(g => g.CreatePoint(request.OriginCoordinates[0], request.OriginCoordinates[1])).Returns(_sampleOriginPoint);
            _mockGeospatialService.Setup(g => g.CreatePoint(request.DestinationCoordinates[0], request.DestinationCoordinates[1])).Returns(_sampleDestPoint);
            
            // Specific mock for distance calculation for this test
            double expectedDistance = 250.5; // km
            _mockGeospatialService.Setup(g => g.CalculateDistanceInKilometers(_sampleOriginPoint, _sampleDestPoint)).Returns(expectedDistance);
            // Mock CreateLineString to return a LineString with the two points
            var expectedLineString = new LineString(new[] { _sampleOriginPoint.Coordinate, _sampleDestPoint.Coordinate }) { SRID = 4326 };
            _mockGeospatialService.Setup(g => g.CreateLineString(It.Is<IEnumerable<Point>>(seq => 
                seq.Count() == 2 && seq.First().Equals(_sampleOriginPoint) && seq.Last().Equals(_sampleDestPoint))))
                .Returns(expectedLineString);


            var result = await _routeService.CreateRouteAsync(request, ownerId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(ownerId, result.OwnerId);
            Assert.Equal(truckId, result.TruckId);
            Assert.Equal(RouteStatus.Planned, result.Status);
            Assert.Equal(1000m, result.TotalCapacityKg);
            Assert.Equal(50m, result.TotalCapacityM3);
            Assert.Equal(expectedDistance, result.EstimatedDistanceKm); // Specific assertion
            int expectedDuration = (int)Math.Round(expectedDistance / 70.0 * 60); // 70.0 is DefaultAverageSpeedKph
            Assert.Equal(expectedDuration, result.EstimatedDurationMinutes); // Specific assertion


            var routeInDb = await _dbContext.Routes.FindAsync(result.Id);
            Assert.NotNull(routeInDb);
            Assert.Equal(ownerId, routeInDb.OwnerId);
            Assert.Equal(expectedLineString, routeInDb.GeometryPath);


            _mockEventPublisher.Verify(ep => ep.PublishRouteCreatedEventAsync(result.Id, It.IsAny<CancellationToken>()), Times.Once);
            _mockLogger.Verify(
                x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Route {result.Id} created successfully")), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task CreateRouteAsync_WithViaPoints_CalculatesCorrectGeometryAndEstimates()
        {
            var ownerId = Guid.NewGuid();
            var truckId = Guid.NewGuid();
            var request = new CreateRouteRequest
            {
                TruckId = truckId,
                OriginCoordinates = new[] { _sampleOriginPoint.X, _sampleOriginPoint.Y },
                ViaPoints = new List<double[]> { new[] { _sampleViaPoint1.X, _sampleViaPoint1.Y }, new[] { _sampleViaPoint2.X, _sampleViaPoint2.Y } },
                DestinationCoordinates = new[] { _sampleDestPoint.X, _sampleDestPoint.Y },
                ScheduledDeparture = DateTimeOffset.UtcNow.AddHours(1),
                ScheduledArrival = DateTimeOffset.UtcNow.AddHours(10),
                IsReturnLeg = false
            };

            _mockTruckServiceClient.Setup(ts => ts.VerifyTruckOwnershipAsync(truckId, ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockTruckServiceClient.Setup(ts => ts.GetTruckCapacityAsync(truckId, It.IsAny<CancellationToken>())).ReturnsAsync((1000m, 50m));
            _mockEventPublisher.Setup(ep => ep.PublishRouteCreatedEventAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _mockGeospatialService.Setup(g => g.CreatePoint(_sampleOriginPoint.X, _sampleOriginPoint.Y)).Returns(_sampleOriginPoint);
            _mockGeospatialService.Setup(g => g.CreatePoint(_sampleViaPoint1.X, _sampleViaPoint1.Y)).Returns(_sampleViaPoint1);
            _mockGeospatialService.Setup(g => g.CreatePoint(_sampleViaPoint2.X, _sampleViaPoint2.Y)).Returns(_sampleViaPoint2);
            _mockGeospatialService.Setup(g => g.CreatePoint(_sampleDestPoint.X, _sampleDestPoint.Y)).Returns(_sampleDestPoint);

            var expectedPointsSequence = new List<Point> { _sampleOriginPoint, _sampleViaPoint1, _sampleViaPoint2, _sampleDestPoint };
            var expectedLineString = new LineString(expectedPointsSequence.Select(p => p.Coordinate).ToArray()) { SRID = 4326 };
            _mockGeospatialService.Setup(g => g.CreateLineString(It.Is<IEnumerable<Point>>(seq => seq.SequenceEqual(expectedPointsSequence))))
                .Returns(expectedLineString);

            _mockGeospatialService.Setup(g => g.CalculateDistanceInKilometers(_sampleOriginPoint, _sampleViaPoint1)).Returns(10.0);
            _mockGeospatialService.Setup(g => g.CalculateDistanceInKilometers(_sampleViaPoint1, _sampleViaPoint2)).Returns(15.0);
            _mockGeospatialService.Setup(g => g.CalculateDistanceInKilometers(_sampleViaPoint2, _sampleDestPoint)).Returns(20.0);
            double expectedTotalDistance = 10.0 + 15.0 + 20.0; // 45.0 km

            var result = await _routeService.CreateRouteAsync(request, ownerId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(expectedTotalDistance, result.EstimatedDistanceKm);
            int expectedDuration = (int)Math.Round(expectedTotalDistance / 70.0 * 60); // 70.0 is DefaultAverageSpeedKph
            Assert.Equal(expectedDuration, result.EstimatedDurationMinutes);

            _mockGeospatialService.Verify(g => g.CreateLineString(It.Is<IEnumerable<Point>>(seq => seq.SequenceEqual(expectedPointsSequence))), Times.Once);
            
            var routeInDb = await _dbContext.Routes.FindAsync(result.Id);
            Assert.NotNull(routeInDb);
            Assert.Equal(JsonSerializer.Serialize(request.ViaPoints), routeInDb.ViaPoints);
            Assert.Equal(expectedLineString, routeInDb.GeometryPath);
        }


        [Fact]
        public async Task CreateRouteAsync_InvalidTimes_ThrowsArgumentException()
        {
            var ownerId = Guid.NewGuid();
            var request = new CreateRouteRequest { ScheduledDeparture = DateTimeOffset.UtcNow.AddHours(2), ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1) }; // Invalid
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _routeService.CreateRouteAsync(request, ownerId, CancellationToken.None));
            Assert.Contains("Departure time must be before arrival time", ex.Message);
        }

        [Fact]
        public async Task CreateRouteAsync_TruckOwnershipVerificationFails_ThrowsUnauthorizedAccessException()
        {
            var ownerId = Guid.NewGuid();
            var truckId = Guid.NewGuid();
            var request = new CreateRouteRequest { 
                TruckId = truckId, 
                OriginCoordinates = new[] { 1.0, 1.0 }, DestinationCoordinates = new[] { 2.0, 2.0 },
                ScheduledDeparture = DateTimeOffset.UtcNow.AddHours(1), ScheduledArrival = DateTimeOffset.UtcNow.AddHours(2)
            };
            _mockTruckServiceClient.Setup(ts => ts.VerifyTruckOwnershipAsync(truckId, ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _routeService.CreateRouteAsync(request, ownerId, CancellationToken.None));
            Assert.Contains($"User {ownerId} does not own truck {truckId}", ex.Message);
        }
        
        [Fact]
        public async Task CreateRouteAsync_InvalidOriginPoint_ThrowsArgumentException()
        {
            var ownerId = Guid.NewGuid();
            var request = new CreateRouteRequest {
                TruckId = Guid.NewGuid(),
                OriginCoordinates = new[] { 200.0, 0.0 }, // Invalid Longitude
                DestinationCoordinates = new[] { 1.0, 1.0 },
                ScheduledDeparture = DateTimeOffset.UtcNow.AddHours(1),
                ScheduledArrival = DateTimeOffset.UtcNow.AddHours(2)
            };
            _mockGeospatialService.Setup(g => g.CreatePoint(200.0, 0.0)).Returns(new Point(200,0));
            _mockGeospatialService.Setup(g => g.ValidatePoint(It.Is<Point>(p => p.X == 200.0))).Returns(false);
             _mockTruckServiceClient.Setup(ts => ts.VerifyTruckOwnershipAsync(request.TruckId, ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(true); // Assume ownership is fine
            _mockTruckServiceClient.Setup(ts => ts.GetTruckCapacityAsync(request.TruckId, It.IsAny<CancellationToken>())).ReturnsAsync((1000m, 50m));


            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _routeService.CreateRouteAsync(request, ownerId, CancellationToken.None));
            Assert.Contains("Invalid coordinates for origin point", ex.Message);
        }


        // --- GetRouteByIdAsync Tests ---
        [Fact]
        public async Task GetRouteByIdAsync_RouteExists_ReturnsRouteDto()
        {
            var routeId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var route = new Route { Id = routeId, OwnerId = ownerId, OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1) };
            _dbContext.Routes.Add(route);
            await _dbContext.SaveChangesAsync();

            var result = await _routeService.GetRouteByIdAsync(routeId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(routeId, result.Id);
        }

        [Fact]
        public async Task GetRouteByIdAsync_RouteDoesNotExist_ReturnsNull()
        {
            var result = await _routeService.GetRouteByIdAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.Null(result);
        }

        // --- GetRoutesAsync Tests ---
        [Fact]
        public async Task GetRoutesAsync_DefaultFilter_ReturnsDtoDefaultPaginatedRoutes() // Renamed
        {
            for (int i = 0; i < 25; i++) // Ensure enough items for PageSize = 20
            {
                _dbContext.Routes.Add(new Route { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i), ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1) });
            }
            await _dbContext.SaveChangesAsync();

            var filter = new RouteFilterRequest(); // DTO default: Page = 1, PageSize = 20
            var results = (await _routeService.GetRoutesAsync(filter, CancellationToken.None)).ToList();

            Assert.NotNull(results);
            Assert.Equal(20, results.Count); // DTO's default PageSize is 20
        }

        [Fact]
        public async Task GetRoutesAsync_NullFilter_ReturnsServiceDefaultPaginatedRoutes() // New Test
        {
            for (int i = 0; i < 15; i++) // Ensure enough items for PageSize = 10
            {
                _dbContext.Routes.Add(new Route { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i), ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1) });
            }
            await _dbContext.SaveChangesAsync();

            var results = (await _routeService.GetRoutesAsync(null, CancellationToken.None)).ToList(); // Pass null filter

            Assert.NotNull(results);
            Assert.Equal(10, results.Count); // Service's internal default PageSize is 10
        }
        
        [Fact]
        public async Task GetRoutesAsync_FilterByOwnerId_ReturnsFilteredRoutes()
        {
            var targetOwnerId = Guid.NewGuid();
            var otherOwnerId = Guid.NewGuid();
            _dbContext.Routes.Add(new Route { Id = Guid.NewGuid(), OwnerId = targetOwnerId, OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1) });
            _dbContext.Routes.Add(new Route { Id = Guid.NewGuid(), OwnerId = targetOwnerId, OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1) });
            _dbContext.Routes.Add(new Route { Id = Guid.NewGuid(), OwnerId = otherOwnerId, OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1) });
            await _dbContext.SaveChangesAsync();

            var filter = new RouteFilterRequest { OwnerId = targetOwnerId };
            var results = (await _routeService.GetRoutesAsync(filter, CancellationToken.None)).ToList();

            Assert.NotNull(results);
            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal(targetOwnerId, r.OwnerId));
        }
        
        // TODO: More GetRoutesAsync tests for other filters, especially proximity. Proximity tests are harder with InMemory.
        // For ST_DWithin, it would require a real PostGIS instance or a sophisticated InMemory spatial mocking.
        // For now, assume the EF Core translation for IsWithinDistance is correct and test other filters.

        // --- UpdateRouteAsync Tests ---
        [Fact]
        public async Task UpdateRouteAsync_WithGeometricChanges_RecalculatesEstimatesAndPath()
        {
            var routeId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var truckId = Guid.NewGuid();

            var initialOrigin = _sampleOriginPoint;
            var initialDestination = _sampleDestPoint;
            var initialViaPoints = new List<double[]> { new[] { _sampleViaPoint1.X, _sampleViaPoint1.Y } };
            var initialViaPointsString = JsonSerializer.Serialize(initialViaPoints);
            
            var initialPointsSequence = new List<Point> { initialOrigin, _sampleViaPoint1, initialDestination };
            var initialLineString = new LineString(initialPointsSequence.Select(p => p.Coordinate).ToArray()) { SRID = 4326 };


            var initialRoute = new Route {
                Id = routeId, OwnerId = ownerId, TruckId = truckId,
                OriginPoint = initialOrigin, DestinationPoint = initialDestination, ViaPoints = initialViaPointsString,
                GeometryPath = initialLineString,
                EstimatedDistanceKm = 30, EstimatedDurationMinutes = 26, // Initial dummy values
                Status = RouteStatus.Planned, ScheduledDeparture = DateTimeOffset.UtcNow.AddHours(1), ScheduledArrival = DateTimeOffset.UtcNow.AddHours(5),
                TotalCapacityKg = 1000m, CapacityAvailableKg = 1000m
            };
            _dbContext.Routes.Add(initialRoute);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(initialRoute).State = EntityState.Detached;

            var newOriginCoords = new[] { 5.0, 15.0 };
            var newOriginPoint = new Point(newOriginCoords[0], newOriginCoords[1]) { SRID = 4326 };
            var newViaPoints = new List<double[]> { new[] { _sampleViaPoint2.X, _sampleViaPoint2.Y } }; // Different via point

            var updateRequest = new UpdateRouteRequest {
                OriginCoordinates = newOriginCoords,
                ViaPoints = newViaPoints,
                // DestinationCoordinates remains the same (_sampleDestPoint)
            };

            _mockTruckServiceClient.Setup(ts => ts.VerifyTruckOwnershipAsync(truckId, ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockEventPublisher.Setup(ep => ep.PublishRouteUpdatedEventAsync(routeId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _mockGeospatialService.Setup(g => g.CreatePoint(newOriginCoords[0], newOriginCoords[1])).Returns(newOriginPoint);
            _mockGeospatialService.Setup(g => g.CreatePoint(_sampleViaPoint2.X, _sampleViaPoint2.Y)).Returns(_sampleViaPoint2);
            // Destination point is not changed, so existing _sampleDestPoint is used by service

            var updatedPointsSequence = new List<Point> { newOriginPoint, _sampleViaPoint2, initialDestination /*_sampleDestPoint*/ };
            var updatedLineString = new LineString(updatedPointsSequence.Select(p => p.Coordinate).ToArray()) { SRID = 4326 };
            _mockGeospatialService.Setup(g => g.CreateLineString(It.Is<IEnumerable<Point>>(seq => 
                seq.ElementAt(0).Equals(newOriginPoint) && 
                seq.ElementAt(1).Equals(_sampleViaPoint2) &&
                seq.ElementAt(2).Equals(initialDestination) && // Ensure correct destination
                seq.Count() == 3)))
                .Returns(updatedLineString);

            _mockGeospatialService.Setup(g => g.CalculateDistanceInKilometers(newOriginPoint, _sampleViaPoint2)).Returns(12.0);
            _mockGeospatialService.Setup(g => g.CalculateDistanceInKilometers(_sampleViaPoint2, initialDestination)).Returns(18.0);
            double expectedNewTotalDistance = 12.0 + 18.0; // 30.0 km

            var result = await _routeService.UpdateRouteAsync(routeId, updateRequest, ownerId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(expectedNewTotalDistance, result.EstimatedDistanceKm);
            int expectedNewDuration = (int)Math.Round(expectedNewTotalDistance / 70.0 * 60);
            Assert.Equal(expectedNewDuration, result.EstimatedDurationMinutes);
            Assert.Equal(newOriginCoords, result.OriginCoordinates);
            Assert.Equal(newViaPoints.Count, result.ViaPoints.Count());
            Assert.Equal(newViaPoints[0][0], result.ViaPoints.First()[0]); // Check first via point coords

            var routeInDb = await _dbContext.Routes.FindAsync(routeId);
            Assert.NotNull(routeInDb);
            Assert.Equal(newOriginPoint, routeInDb.OriginPoint);
            Assert.Equal(JsonSerializer.Serialize(newViaPoints), routeInDb.ViaPoints);
            Assert.Equal(updatedLineString, routeInDb.GeometryPath);
            Assert.Equal(expectedNewTotalDistance, routeInDb.EstimatedDistanceKm);
            Assert.Equal(expectedNewDuration, routeInDb.EstimatedDurationMinutes);

            _mockEventPublisher.Verify(ep => ep.PublishRouteUpdatedEventAsync(routeId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateRouteAsync_ValidUpdate_ReturnsUpdatedDtoAndPublishesEvents()
        {
            var routeId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var originalDeparture = DateTimeOffset.UtcNow.AddHours(1);
            var route = new Route { 
                Id = routeId, OwnerId = ownerId, TruckId = Guid.NewGuid(), 
                OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, 
                Status = RouteStatus.Planned, ScheduledDeparture = originalDeparture, ScheduledArrival = originalDeparture.AddHours(4)
            };
            _dbContext.Routes.Add(route);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(route).State = EntityState.Detached; // Detach to simulate loading in a new request


            var newDeparture = DateTimeOffset.UtcNow.AddHours(2);
            var newStatus = RouteStatus.InProgress;
            var updateRequest = new UpdateRouteRequest { ScheduledDeparture = newDeparture, Status = newStatus };

            _mockTruckServiceClient.Setup(ts => ts.VerifyTruckOwnershipAsync(route.TruckId, ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockEventPublisher.Setup(ep => ep.PublishRouteUpdatedEventAsync(routeId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockEventPublisher.Setup(ep => ep.PublishRouteStatusUpdatedEventAsync(routeId, RouteStatus.Planned, newStatus, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await _routeService.UpdateRouteAsync(routeId, updateRequest, ownerId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(newDeparture, result.ScheduledDeparture);
            Assert.Equal(newStatus, result.Status);

            var updatedRouteInDb = await _dbContext.Routes.FindAsync(routeId);
            Assert.NotNull(updatedRouteInDb);
            Assert.Equal(newDeparture, updatedRouteInDb.ScheduledDeparture);
            Assert.Equal(newStatus, updatedRouteInDb.Status);

            _mockEventPublisher.Verify(ep => ep.PublishRouteUpdatedEventAsync(routeId, It.IsAny<CancellationToken>()), Times.Once);
            _mockEventPublisher.Verify(ep => ep.PublishRouteStatusUpdatedEventAsync(routeId, RouteStatus.Planned, newStatus, It.IsAny<CancellationToken>()), Times.Once);
        }
        
        [Fact]
        public async Task UpdateRouteAsync_UnauthorizedOwner_ThrowsUnauthorizedAccessException()
        {
            var routeId = Guid.NewGuid();
            var actualOwnerId = Guid.NewGuid();
            var wrongOwnerId = Guid.NewGuid();
            var route = new Route { Id = routeId, OwnerId = actualOwnerId, TruckId = Guid.NewGuid(), OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1) };
            _dbContext.Routes.Add(route);
            await _dbContext.SaveChangesAsync();

            var updateRequest = new UpdateRouteRequest { Notes = "test update" };
            _mockTruckServiceClient.Setup(ts => ts.VerifyTruckOwnershipAsync(route.TruckId, wrongOwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _routeService.UpdateRouteAsync(routeId, updateRequest, wrongOwnerId, CancellationToken.None));
        }

        // --- UpdateRouteCapacityAsync Tests ---
        [Fact]
        public async Task UpdateRouteCapacityAsync_ValidChange_UpdatesCapacityAndStatusAndPublishesEvents()
        {
            var routeId = Guid.NewGuid();
            var route = new Route { 
                Id = routeId, OwnerId = Guid.NewGuid(), TruckId = Guid.NewGuid(),
                TotalCapacityKg = 1000m, CapacityAvailableKg = 500m, Status = RouteStatus.BookedPartial,
                OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1)
            };
            _dbContext.Routes.Add(route);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(route).State = EntityState.Detached;

            var capacityChangeRequest = new UpdateRouteCapacityRequest { CapacityChangeKg = -500m }; // Empties the truck

            _mockEventPublisher.Setup(ep => ep.PublishRouteCapacityChangedEventAsync(routeId, 500m, 0m, null, null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockEventPublisher.Setup(ep => ep.PublishRouteStatusUpdatedEventAsync(routeId, RouteStatus.BookedPartial, RouteStatus.BookedFull, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await _routeService.UpdateRouteCapacityAsync(routeId, capacityChangeRequest, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(0m, result.CapacityAvailableKg);
            Assert.Equal(RouteStatus.BookedFull, result.Status);

            _mockEventPublisher.Verify(ep => ep.PublishRouteCapacityChangedEventAsync(routeId, 500m, 0m, null, null, It.IsAny<CancellationToken>()), Times.Once);
            _mockEventPublisher.Verify(ep => ep.PublishRouteStatusUpdatedEventAsync(routeId, RouteStatus.BookedPartial, RouteStatus.BookedFull, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(1000, 50, 500, 25, -250, -10, RouteStatus.BookedPartial, RouteStatus.BookedPartial)] // Kg partial, M3 partial -> BookedPartial
        [InlineData(1000, 50, 500, 25, -500, -25, RouteStatus.BookedPartial, RouteStatus.BookedFull)]   // Kg full, M3 full -> BookedFull
        [InlineData(1000, 50, 0, 0, 500, 0, RouteStatus.BookedFull, RouteStatus.BookedPartial)]      // From BothFull, Kg restored, M3 still zero -> BookedPartial
        [InlineData(1000, 50, 0, 0, 1000, 50, RouteStatus.BookedFull, RouteStatus.Planned)]        // From BothFull, Kg restored, M3 restored -> Planned
        [InlineData(1000, 50, 1000, 10, 0, 40, RouteStatus.BookedPartial, RouteStatus.Planned)]      // From M3Partial, M3 restored (Kg already full) -> Planned
        [InlineData(1000, null, 500, null, -250, null, RouteStatus.BookedPartial, RouteStatus.BookedPartial)] // TotalM3 null, Kg partial -> BookedPartial
        [InlineData(1000, null, 500, null, -500, null, RouteStatus.BookedPartial, RouteStatus.BookedFull)]   // TotalM3 null, Kg full -> BookedFull
        [InlineData(1000, null, 0, null, 1000, null, RouteStatus.BookedFull, RouteStatus.Planned)]        // TotalM3 null, Kg restored -> Planned
        public async Task UpdateRouteCapacityAsync_WithVolumeLogic_CorrectlyUpdatesStatus(
            decimal totalKg, decimal? totalM3, decimal initialKg, decimal? initialM3,
            decimal changeKg, decimal? changeM3,
            RouteStatus initialStatus, RouteStatus expectedFinalStatus)
        {
            var routeId = Guid.NewGuid();
            var route = new Route
            {
                Id = routeId, OwnerId = Guid.NewGuid(), TruckId = Guid.NewGuid(),
                TotalCapacityKg = totalKg, TotalCapacityM3 = totalM3,
                CapacityAvailableKg = initialKg, CapacityAvailableM3 = initialM3,
                Status = initialStatus,
                OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint,
                ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1)
            };
            _dbContext.Routes.Add(route);
            await _dbContext.SaveChangesAsync();
            _dbContext.Entry(route).State = EntityState.Detached;

            var capacityChangeRequest = new UpdateRouteCapacityRequest { CapacityChangeKg = changeKg, CapacityChangeM3 = changeM3 };

            decimal expectedNewKg = Math.Clamp(initialKg + changeKg, 0, totalKg);
            decimal? expectedNewM3 = null;
            if (totalM3.HasValue) // Only calculate expectedNewM3 if totalM3 is defined
            {
                expectedNewM3 = Math.Clamp((initialM3 ?? 0) + (changeM3 ?? 0), 0, totalM3.Value);
            }
            else if (changeM3.HasValue && (initialM3??0) + changeM3.Value != 0) // if totalM3 is null, newM3 should also be null unless the change makes it zero.
            {
                 // This case implies an attempt to set M3 when TotalM3 is null.
                 // The service logic sets CapacityAvailableM3 to null if TotalCapacityM3 is null.
                 expectedNewM3 = null;
            }


            _mockEventPublisher.Setup(ep => ep.PublishRouteCapacityChangedEventAsync(routeId, initialKg, expectedNewKg, initialM3, expectedNewM3, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            if (expectedFinalStatus != initialStatus)
            {
                _mockEventPublisher.Setup(ep => ep.PublishRouteStatusUpdatedEventAsync(routeId, initialStatus, expectedFinalStatus, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
            }

            var result = await _routeService.UpdateRouteCapacityAsync(routeId, capacityChangeRequest, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(expectedNewKg, result.CapacityAvailableKg);
            if (totalM3.HasValue)
            {
                Assert.Equal(expectedNewM3, result.CapacityAvailableM3);
            }
            else
            {
                Assert.Null(result.CapacityAvailableM3); // Ensure M3 remains null if TotalM3 is null
            }
            Assert.Equal(expectedFinalStatus, result.Status);

            _mockEventPublisher.Verify(ep => ep.PublishRouteCapacityChangedEventAsync(routeId, initialKg, expectedNewKg, initialM3, expectedNewM3, It.IsAny<CancellationToken>()), Times.Once);
            if (expectedFinalStatus != initialStatus)
            {
                _mockEventPublisher.Verify(ep => ep.PublishRouteStatusUpdatedEventAsync(routeId, initialStatus, expectedFinalStatus, It.IsAny<CancellationToken>()), Times.Once);
            }
            else
            {
                _mockEventPublisher.Verify(ep => ep.PublishRouteStatusUpdatedEventAsync(routeId, It.IsAny<RouteStatus>(), It.IsAny<RouteStatus>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }


        // --- CancelRouteAsync Tests ---
        [Fact]
        public async Task CancelRouteAsync_ValidRequest_CancelsRouteAndPublishesEvent()
        {
            var routeId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var route = new Route { Id = routeId, OwnerId = ownerId, TruckId = Guid.NewGuid(), Status = RouteStatus.Planned, OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1) };
            _dbContext.Routes.Add(route);
            await _dbContext.SaveChangesAsync();

            _mockTruckServiceClient.Setup(ts => ts.VerifyTruckOwnershipAsync(route.TruckId, ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockEventPublisher.Setup(ep => ep.PublishRouteStatusUpdatedEventAsync(routeId, RouteStatus.Planned, RouteStatus.Cancelled, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var success = await _routeService.CancelRouteAsync(routeId, ownerId, CancellationToken.None);

            Assert.True(success);
            var cancelledRoute = await _dbContext.Routes.FindAsync(routeId);
            Assert.Equal(RouteStatus.Cancelled, cancelledRoute.Status);
            _mockEventPublisher.Verify(ep => ep.PublishRouteStatusUpdatedEventAsync(routeId, RouteStatus.Planned, RouteStatus.Cancelled, It.IsAny<CancellationToken>()), Times.Once);
        }
        
        [Fact]
        public async Task CancelRouteAsync_AlreadyCancelled_ReturnsTrue()
        {
            var routeId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var route = new Route { Id = routeId, OwnerId = ownerId, TruckId = Guid.NewGuid(), Status = RouteStatus.Cancelled, OriginPoint = _sampleOriginPoint, DestinationPoint = _sampleDestPoint, ScheduledDeparture = DateTimeOffset.UtcNow, ScheduledArrival = DateTimeOffset.UtcNow.AddHours(1) };
            _dbContext.Routes.Add(route);
            await _dbContext.SaveChangesAsync();

            _mockTruckServiceClient.Setup(ts => ts.VerifyTruckOwnershipAsync(route.TruckId, ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            
            var success = await _routeService.CancelRouteAsync(routeId, ownerId, CancellationToken.None);

            Assert.True(success);
            _mockEventPublisher.Verify(ep => ep.PublishRouteStatusUpdatedEventAsync(It.IsAny<Guid>(), It.IsAny<RouteStatus>(), It.IsAny<RouteStatus>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
