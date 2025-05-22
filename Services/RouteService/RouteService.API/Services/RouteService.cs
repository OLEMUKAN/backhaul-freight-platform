using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using RouteService.API.Data;
using RouteService.API.Services.Interfaces;
using MessageContracts.Enums; // For RouteStatus
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RouteService.API.Models;
using RouteService.API.Models.DTOs;

namespace RouteService.API.Services
{
    public class RouteService : IRouteService
    {
        private readonly RouteDbContext _context;
        private readonly IGeospatialService _geospatialService;
        private readonly ITruckServiceClient _truckServiceClient;
        private readonly IEventPublisher _eventPublisher;
        private readonly IMapper _mapper;
        private readonly ILogger<RouteService> _logger;
        private const double DefaultAverageSpeedKph = 70.0;

        public RouteService(
            RouteDbContext context,
            IGeospatialService geospatialService,
            ITruckServiceClient truckServiceClient,
            IEventPublisher eventPublisher,
            IMapper mapper,
            ILogger<RouteService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _geospatialService = geospatialService ?? throw new ArgumentNullException(nameof(geospatialService));
            _truckServiceClient = truckServiceClient ?? throw new ArgumentNullException(nameof(truckServiceClient));
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }        public async Task<RouteDto> CreateRouteAsync(CreateRouteRequest request, Guid ownerId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Attempting to create route for Owner: {OwnerId}, Truck: {TruckId}", ownerId, request.TruckId);
            
            if (!request.AreTimesValid())
            {
                _logger.LogWarning("Invalid times provided for route creation by Owner: {OwnerId}. Departure: {Departure}, Arrival: {Arrival}", ownerId, request.DepartureTime, request.ArrivalTime);
                throw new ArgumentException("Departure time must be before arrival time and both must be in the future.");
            }
            if (!request.AreCoordinatesValid())
            {
                _logger.LogWarning("Invalid coordinates provided for route creation by Owner: {OwnerId}", ownerId);
                throw new ArgumentException("Origin and destination coordinates must be provided.");
            }
            
            // AutoMapper will use IGeospatialService from its context if configured that way,
            // otherwise, manual creation before mapping or custom resolver needed.
            // Assuming MappingProfile is set up to use IGeospatialService via context.
            var route = _mapper.Map<Models.Route>(request, opts => opts.Items["GeospatialService"] = _geospatialService);
            route.OwnerId = ownerId;

            // Points are created by AutoMapper via MappingProfile
            if (!_geospatialService.ValidatePoint(route.OriginPoint)) 
            {
                 _logger.LogWarning("Invalid origin point for route creation by Owner: {OwnerId}. Coordinates: {Coords}", ownerId, request.OriginCoordinates);
                 throw new ArgumentException("Invalid coordinates for origin point.");
            }
            if (!_geospatialService.ValidatePoint(route.DestinationPoint))
            {
                _logger.LogWarning("Invalid destination point for route creation by Owner: {OwnerId}. Coordinates: {Coords}", ownerId, request.DestinationCoordinates);
                throw new ArgumentException("Invalid coordinates for destination point.");
            }


            if (!await _truckServiceClient.VerifyTruckOwnershipAsync(request.TruckId, ownerId, cancellationToken))
            {
                _logger.LogWarning("Truck ownership verification failed for TruckId: {TruckId}, OwnerId: {OwnerId}", request.TruckId, ownerId);
                throw new UnauthorizedAccessException($"User {ownerId} does not own truck {request.TruckId}.");
            }

            (var capacityKg, var capacityM3) = await _truckServiceClient.GetTruckCapacityAsync(request.TruckId, cancellationToken);
            if (capacityKg <= 0) 
            {
                _logger.LogWarning("Failed to retrieve valid capacity for TruckId: {TruckId}. CapacityKg: {CapacityKg}", request.TruckId, capacityKg);
                throw new ArgumentException($"Could not retrieve valid truck capacity information for truck {request.TruckId}. Ensure truck exists and has capacity defined.");
            }

            route.TotalCapacityKg = capacityKg;
            route.TotalCapacityM3 = capacityM3;
            route.CapacityAvailableKg = capacityKg;
            route.CapacityAvailableM3 = capacityM3;

            List<Point> allPointsForPath = new List<Point> { route.OriginPoint };
            if (request.ViaPoints != null && request.ViaPoints.Any())
            {
                foreach (var viaCoord in request.ViaPoints)
                {
                    if (viaCoord == null || viaCoord.Length != 2) 
                    {
                        _logger.LogWarning("Invalid coordinate pair in ViaPoints for Owner: {OwnerId}", ownerId);
                        throw new ArgumentException("Invalid coordinate pair in ViaPoints.");
                    }
                    var viaPoint = _geospatialService.CreatePoint(viaCoord[0], viaCoord[1]);
                    if (!_geospatialService.ValidatePoint(viaPoint))
                    {
                         _logger.LogWarning("Invalid ViaPoint for Owner: {OwnerId}. Coordinates: {Coords}", ownerId, viaCoord);
                         throw new ArgumentException($"Invalid ViaPoint provided: Longitude {viaCoord[0]}, Latitude {viaCoord[1]}.");
                    }
                    allPointsForPath.Add(viaPoint);
                }
                // route.ViaPoints (string) is mapped by AutoMapper by serializing request.ViaPoints
            }
            allPointsForPath.Add(route.DestinationPoint);
            route.GeometryPath = _geospatialService.CreateLineString(allPointsForPath);

            route.EstimatedDistanceKm = 0;
            if (route.GeometryPath != null && route.GeometryPath.Coordinates.Length > 1)
            {
                for (int i = 0; i < route.GeometryPath.Coordinates.Length - 1; i++)
                {
                    var p1 = _geospatialService.CreatePoint(route.GeometryPath.Coordinates[i].X, route.GeometryPath.Coordinates[i].Y);
                    var p2 = _geospatialService.CreatePoint(route.GeometryPath.Coordinates[i + 1].X, route.GeometryPath.Coordinates[i + 1].Y);                    route.EstimatedDistanceKm += (decimal)_geospatialService.CalculateDistanceInKilometers(p1, p2);
                }
            }
            
            route.EstimatedDistanceKm = Math.Round(route.EstimatedDistanceKm, 2);
            route.EstimatedDurationMinutes = (int)Math.Round((double)route.EstimatedDistanceKm / DefaultAverageSpeedKph * 60);

            route.Id = Guid.NewGuid();
            route.Status = RouteStatus.Planned;
            var now = DateTimeOffset.UtcNow;
            route.CreatedAt = now;
            route.UpdatedAt = now;

            _context.Routes.Add(route);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Route {RouteId} created successfully for Owner: {OwnerId}, Truck: {TruckId}.", route.Id, ownerId, request.TruckId);

            await _eventPublisher.PublishRouteCreatedEventAsync(route.Id, cancellationToken);

            return _mapper.Map<RouteDto>(route, opts => opts.Items["GeospatialService"] = _geospatialService);
        }

        public async Task<RouteDto?> GetRouteByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching route by Id: {RouteId}", id);
            var route = await _context.Routes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (route == null)
            {
                 _logger.LogWarning("Route with Id: {RouteId} not found.", id);
                 return null;
            }
            return _mapper.Map<RouteDto>(route, opts => opts.Items["GeospatialService"] = _geospatialService);
        }

        public async Task<IEnumerable<RouteDto>> GetRoutesAsync(RouteFilterRequest? filter = null, CancellationToken cancellationToken = default)
        {
             _logger.LogInformation("Fetching routes with filter: {@RouteFilterRequest}", filter);
            var query = _context.Routes.AsQueryable();

            if (filter != null)
            {                if (filter.OwnerId.HasValue) query = query.Where(r => r.OwnerId == filter.OwnerId.Value);
                if (filter.TruckId.HasValue) query = query.Where(r => r.TruckId == filter.TruckId.Value);
                if (filter.IsReturnLeg.HasValue) query = query.Where(r => r.IsReturnLeg == filter.IsReturnLeg.Value);
                if (filter.Status.HasValue) query = query.Where(r => r.Status == (RouteStatus)filter.Status.Value);
                if (filter.MinCapacityKg.HasValue) query = query.Where(r => r.CapacityAvailableKg >= filter.MinCapacityKg.Value);if (filter.MinCapacityM3.HasValue) query = query.Where(r => r.CapacityAvailableM3.HasValue && r.CapacityAvailableM3.Value >= filter.MinCapacityM3.Value);
                if (filter.DepartAfter.HasValue) query = query.Where(r => r.DepartureTime >= filter.DepartAfter.Value);
                if (filter.DepartBefore.HasValue) query = query.Where(r => r.DepartureTime <= filter.DepartBefore.Value);
                if (filter.ArriveAfter.HasValue) query = query.Where(r => r.ArrivalTime >= filter.ArriveAfter.Value);
                if (filter.ArriveBefore.HasValue) query = query.Where(r => r.ArrivalTime <= filter.ArriveBefore.Value);

                if (filter.NearOrigin != null && filter.NearOrigin.Length == 2 && filter.OriginRadiusKm.HasValue && filter.OriginRadiusKm.Value > 0)
                {
                    var searchPoint = _geospatialService.CreatePoint(filter.NearOrigin[0], filter.NearOrigin[1]);
                    // EF Core PostGIS uses meters for distance, so convert km to meters.
                    query = query.Where(r => r.OriginPoint.IsWithinDistance(searchPoint, filter.OriginRadiusKm.Value * 1000));
                }
                if (filter.NearDestination != null && filter.NearDestination.Length == 2 && filter.DestinationRadiusKm.HasValue && filter.DestinationRadiusKm.Value > 0)
                {
                    var searchPoint = _geospatialService.CreatePoint(filter.NearDestination[0], filter.NearDestination[1]);
                    query = query.Where(r => r.DestinationPoint.IsWithinDistance(searchPoint, filter.DestinationRadiusKm.Value * 1000));
                }

                // Filtering logic based on filter properties remains here
            }

            // Standardized pagination section
            int page = filter?.Page ?? 1; // Default to page 1 if filter or filter.Page is null
            int pageSize = filter?.PageSize ?? 10; // Default to page size 10 if filter or filter.PageSize is null/0

            if (page <= 0) page = 1; // Ensure page is positive
            if (pageSize <= 0) pageSize = 10; // Ensure pageSize is positive, default to 10
            // Optional: Add a max page size cap
            // const int maxPageSize = 100;
            // if (pageSize > maxPageSize) pageSize = maxPageSize;

            query = query.OrderByDescending(r => r.CreatedAt) // Apply ordering BEFORE pagination
                         .Skip((page - 1) * pageSize)
                         .Take(pageSize);
            
            var routes = await query.ToListAsync(cancellationToken);
            _logger.LogInformation("Found {Count} routes matching filter. Page: {Page}, PageSize: {PageSize}", routes.Count, page, pageSize);
            return _mapper.Map<IEnumerable<RouteDto>>(routes, opts => opts.Items["GeospatialService"] = _geospatialService);
        }

        public async Task<RouteDto?> UpdateRouteAsync(Guid id, UpdateRouteRequest request, Guid ownerId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Attempting to update route {RouteId} by Owner: {OwnerId}", id, ownerId);
            var route = await _context.Routes.FindAsync(new object[] { id }, cancellationToken);
            if (route == null)
            {
                _logger.LogWarning("Route {RouteId} not found for update by Owner: {OwnerId}", id, ownerId);
                return null;
            }

            // Verify ownership using the route's current TruckId.
            if (!await _truckServiceClient.VerifyTruckOwnershipAsync(route.TruckId, ownerId, cancellationToken))
            {
                 _logger.LogWarning("Ownership verification failed for updating Route {RouteId}. Truck {TruckId}, Owner {OwnerId}", id, route.TruckId, ownerId);
                 throw new UnauthorizedAccessException($"User {ownerId} is not authorized to update route {id}.");
            }

            var oldStatus = route.Status;
            var oldCapacityKg = route.CapacityAvailableKg; // For potential event later
            var oldCapacityM3 = route.CapacityAvailableM3; // For potential event later

            // Apply updates from request. AutoMapper handles nulls for properties not in request.
            _mapper.Map(request, route, opts => opts.Items["GeospatialService"] = _geospatialService);

            bool geometryNeedsRecalculation = false;
            if (request.OriginCoordinates != null) 
            {
                route.OriginPoint = _geospatialService.CreatePoint(request.OriginCoordinates[0], request.OriginCoordinates[1]);
                if(!_geospatialService.ValidatePoint(route.OriginPoint)) throw new ArgumentException("Invalid origin coordinates in update request.");
                geometryNeedsRecalculation = true;
            }
            if (request.DestinationCoordinates != null)
            {
                route.DestinationPoint = _geospatialService.CreatePoint(request.DestinationCoordinates[0], request.DestinationCoordinates[1]);
                if(!_geospatialService.ValidatePoint(route.DestinationPoint)) throw new ArgumentException("Invalid destination coordinates in update request.");
                geometryNeedsRecalculation = true;
            }
            if (request.ViaPoints != null) // ViaPoints are fully replaced if provided
            {
                 // AutoMapper should handle serialization of request.ViaPoints (double[][]) to route.ViaPoints (string)
                 // If not, manual serialization: route.ViaPoints = JsonSerializer.Serialize(request.ViaPoints);
                 geometryNeedsRecalculation = true;
            }

            if (geometryNeedsRecalculation)
            {
                List<Point> allPointsForPath = new List<Point> { route.OriginPoint };
                if (!string.IsNullOrEmpty(route.ViaPoints))
                {
                    var viaCoordsList = JsonSerializer.Deserialize<IEnumerable<double[]>>(route.ViaPoints);
                    if (viaCoordsList != null)
                    {
                        foreach (var viaCoord in viaCoordsList)
                        {
                             if (viaCoord == null || viaCoord.Length != 2) throw new ArgumentException("Invalid coordinate pair in ViaPoints during update.");
                            var viaPoint = _geospatialService.CreatePoint(viaCoord[0], viaCoord[1]);
                            if (!_geospatialService.ValidatePoint(viaPoint)) throw new ArgumentException($"Invalid ViaPoint during update: Longitude {viaCoord[0]}, Latitude {viaCoord[1]}.");
                            allPointsForPath.Add(viaPoint);
                        }
                    }
                }
                allPointsForPath.Add(route.DestinationPoint);
                route.GeometryPath = _geospatialService.CreateLineString(allPointsForPath);

                route.EstimatedDistanceKm = 0;
                if (route.GeometryPath != null && route.GeometryPath.Coordinates.Length > 1)
                {
                    for (int i = 0; i < route.GeometryPath.Coordinates.Length - 1; i++)
                    {
                        var p1 = _geospatialService.CreatePoint(route.GeometryPath.Coordinates[i].X, route.GeometryPath.Coordinates[i].Y);
                        var p2 = _geospatialService.CreatePoint(route.GeometryPath.Coordinates[i + 1].X, route.GeometryPath.Coordinates[i + 1].Y);                        route.EstimatedDistanceKm += (decimal)_geospatialService.CalculateDistanceInKilometers(p1, p2);
                    }
                }
                route.EstimatedDistanceKm = Math.Round(route.EstimatedDistanceKm, 2);
                route.EstimatedDurationMinutes = (int)Math.Round((double)route.EstimatedDistanceKm / DefaultAverageSpeedKph * 60);
            }
              // Validate times if they were part of the request
            if (request.DepartureTime.HasValue || request.ArrivalTime.HasValue) {
                // Create a temporary request DTO with potentially updated times to validate
                var tempValidationRequest = new CreateRouteRequest { 
                    DepartureTime = route.DepartureTime, 
                    ArrivalTime = route.ArrivalTime 
                };
                if (!tempValidationRequest.AreTimesValid()) {
                     _logger.LogWarning("Invalid times provided for route update {RouteId}. Departure: {Departure}, Arrival: {Arrival}", id, route.DepartureTime, route.ArrivalTime);
                     throw new ArgumentException("Departure time must be before arrival time and both must be in the future.");
                }
            }

            route.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Route {RouteId} updated successfully by Owner: {OwnerId}.", route.Id, ownerId);

            await _eventPublisher.PublishRouteUpdatedEventAsync(route.Id, cancellationToken);
            if (request.Status.HasValue && route.Status != oldStatus) // Only publish if status was in the request and changed
            {
                await _eventPublisher.PublishRouteStatusUpdatedEventAsync(route.Id, oldStatus, route.Status, cancellationToken);
            }
            // Capacity might have changed if truckId changed, but this method does not handle truckId changes.
            // If TotalCapacityKg or TotalCapacityM3 were part of UpdateRouteRequest (they are not per DTO),
            // then a capacity changed event might be needed here.

            return _mapper.Map<RouteDto>(route, opts => opts.Items["GeospatialService"] = _geospatialService);
        }

        public async Task<RouteDto?> UpdateRouteCapacityAsync(Guid id, UpdateRouteCapacityRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Attempting to update capacity for route {RouteId} with changes: Kg={CapacityChangeKg}, M3={CapacityChangeM3}", 
                id, request.CapacityChangeKg, request.CapacityChangeM3);
            var route = await _context.Routes.FindAsync(new object[] { id }, cancellationToken);
            if (route == null)
            {
                _logger.LogWarning("Route {RouteId} not found for capacity update.", id);
                return null; // Or throw KeyNotFoundException
            }

            var oldCapacityKg = route.CapacityAvailableKg;
            var oldCapacityM3 = route.CapacityAvailableM3;
            var oldStatus = route.Status;

            route.CapacityAvailableKg += request.CapacityChangeKg;
            if (request.CapacityChangeM3.HasValue)
            {
                route.CapacityAvailableM3 = (route.CapacityAvailableM3 ?? 0) + request.CapacityChangeM3.Value;
            }

            // Validate and clamp capacities
            if (route.CapacityAvailableKg < 0) route.CapacityAvailableKg = 0;
            if (route.CapacityAvailableKg > route.TotalCapacityKg) route.CapacityAvailableKg = route.TotalCapacityKg;
            
            if (route.CapacityAvailableM3.HasValue)
            {
                if (route.CapacityAvailableM3 < 0) route.CapacityAvailableM3 = 0;
                if (route.TotalCapacityM3.HasValue)
                {
                    if (route.CapacityAvailableM3 > route.TotalCapacityM3)
                    {
                        route.CapacityAvailableM3 = route.TotalCapacityM3;
                    }
                }
                else // TotalCapacityM3 is null
                {
                    route.CapacityAvailableM3 = null; // Available M3 cannot be set if total M3 is not defined
                }
            }

            // Update status based on capacity
            // Using a small epsilon for decimal comparison (e.g., 0.01m for kg)
            // Capture the status before any changes due to capacity update, to compare later for event publishing.
            // The 'oldStatus' variable already holds this from before capacity modifications.

            bool isKgEffectivelyZero = route.CapacityAvailableKg <= 0.01m;
            bool isM3EffectivelyZero = route.TotalCapacityM3.HasValue && route.CapacityAvailableM3.HasValue && route.CapacityAvailableM3.Value <= 0.01m;
            
            // Determine if the route is booked full
            // If TotalCapacityM3 is not defined, fullness is determined by Kg only.
            // If TotalCapacityM3 is defined, both Kg and M3 must be effectively zero to be BookedFull.
            bool isBookedFull;
            if (route.TotalCapacityM3.HasValue)
            {
                isBookedFull = isKgEffectivelyZero && isM3EffectivelyZero;
            }
            else // M3 is not a factor for capacity
            {
                isBookedFull = isKgEffectivelyZero;
            }

            if (isBookedFull)
            {
                if (route.Status != RouteStatus.Cancelled && route.Status != RouteStatus.Completed)
                    route.Status = RouteStatus.BookedFull;
            }
            else // Not BookedFull, so either BookedPartial or Planned
            {
                bool isKgLessThanTotal = route.CapacityAvailableKg < route.TotalCapacityKg;
                // Consider M3 less than total only if TotalCapacityM3 is defined.
                bool isM3LessThanTotal = route.TotalCapacityM3.HasValue && 
                                         route.CapacityAvailableM3.HasValue && // Ensure CapacityAvailableM3 also has value
                                         route.CapacityAvailableM3.Value < route.TotalCapacityM3.Value;

                if (isKgLessThanTotal || (route.TotalCapacityM3.HasValue && isM3LessThanTotal)) // If EITHER is less than total (and M3 is a factor for the OR part)
                {
                    if (route.Status != RouteStatus.Cancelled && route.Status != RouteStatus.Completed && route.Status != RouteStatus.InProgress)
                        route.Status = RouteStatus.BookedPartial;
                }
                else // All relevant capacities are at their total
                {
                    // Only transition to Planned if it was previously BookedFull or BookedPartial
                    // and not in a conflicting state like InProgress, Completed, or Cancelled.
                    if ((oldStatus == RouteStatus.BookedFull || oldStatus == RouteStatus.BookedPartial) &&
                        (route.Status != RouteStatus.InProgress && route.Status != RouteStatus.Completed && route.Status != RouteStatus.Cancelled))
                    {
                        route.Status = RouteStatus.Planned;
                    }
                    // If oldStatus was already Planned and capacity remains full, it stays Planned.
                    // If oldStatus was InProgress and capacity becomes full (restored), it stays InProgress.
                }
            }
            
            route.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Capacity updated for Route {RouteId}. New Available Kg: {NewKg}, New Available M3: {NewM3}. Status: {Status}", 
                id, route.CapacityAvailableKg, route.CapacityAvailableM3, route.Status);

            await _eventPublisher.PublishRouteCapacityChangedEventAsync(route.Id, oldCapacityKg, route.CapacityAvailableKg, oldCapacityM3, route.CapacityAvailableM3, cancellationToken);
            if (route.Status != oldStatus)
            {
                await _eventPublisher.PublishRouteStatusUpdatedEventAsync(route.Id, oldStatus, route.Status, cancellationToken);
            }
            
            return _mapper.Map<RouteDto>(route, opts => opts.Items["GeospatialService"] = _geospatialService);
        }

        public async Task<bool> CancelRouteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Attempting to cancel route {RouteId} by Owner: {OwnerId}", id, ownerId);
            var route = await _context.Routes.FindAsync(new object[] { id }, cancellationToken);
            if (route == null)
            {
                _logger.LogWarning("Route {RouteId} not found for cancellation by Owner: {OwnerId}.", id, ownerId);
                return false;
            }

            if (!await _truckServiceClient.VerifyTruckOwnershipAsync(route.TruckId, ownerId, cancellationToken))
            {
                 _logger.LogWarning("Ownership verification failed for cancelling Route {RouteId}. Truck {TruckId}, Owner {OwnerId}", id, route.TruckId, ownerId);
                 throw new UnauthorizedAccessException($"User {ownerId} is not authorized to cancel route {id}.");
            }

            if (route.Status == RouteStatus.Cancelled)
            {
                _logger.LogInformation("Route {RouteId} is already cancelled.", id);
                return true; // Idempotency: Already in desired state.
            }
            
            // Business logic: e.g., cannot cancel if RouteStatus is InProgress, Completed.
            // For this implementation, we allow cancellation from any non-cancelled state as per prompt.
            // if (route.Status == RouteStatus.InProgress || route.Status == RouteStatus.Completed)
            // {
            //     _logger.LogWarning("Cannot cancel route {RouteId} as it is in status {Status}.", id, route.Status);
            //     throw new InvalidOperationException($"Cannot cancel route in {route.Status} state.");
            // }

            var oldStatus = route.Status;
            route.Status = RouteStatus.Cancelled;
            route.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Route {RouteId} cancelled successfully by Owner: {OwnerId}.", id, ownerId);

            await _eventPublisher.PublishRouteStatusUpdatedEventAsync(route.Id, oldStatus, RouteStatus.Cancelled, cancellationToken);
            return true;
        }
    }
}
