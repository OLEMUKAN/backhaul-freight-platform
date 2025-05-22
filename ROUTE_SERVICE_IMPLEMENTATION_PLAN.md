# Route Service Implementation Plan

## Overview
The Route Service manages planned routes (outbound and return) for trucks. It stores the intended path, timing, and availability windows for truck routes. This service is critical for the Mapbox frontend integration and the matching algorithm.

## Tech Stack
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL with PostGIS extension
- MassTransit/RabbitMQ for event handling
- Mapbox Directions API (optional, for distance/duration calculation)

## Implementation Checklist

### 1. Project Setup
- [x] Create solution structure
  - [x] Create RouteService.sln solution
  - [x] Create RouteService.API project (ASP.NET Core Web API)
  - [x] Set up project dependencies and NuGet packages:
    - [x] Entity Framework Core and Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite
    - [x] MassTransit and MassTransit.RabbitMQ
    - [x] Serilog for logging
    - [x] Swashbuckle for API documentation
    - [x] Polly for resilience
  - [x] Reference SharedLibraries (MessageContracts, ServiceDiscovery)
  - [x] Configure appsettings.json with appropriate settings (database, RabbitMQ, etc.)
- [ ] Configure PostgreSQL with PostGIS
  - [x] Set up connection string in appsettings.json
  - [ ] Configure Entity Framework Core for PostgreSQL with PostGIS
  - [ ] Add DbContext registration in Program.cs

### 2. Define Shared Message Contracts
- [x] Create message contracts in SharedLibraries/MessageContracts/Events/Route/
  - [x] RouteCreatedEvent (inherit from EventBase)
  - [x] RouteUpdatedEvent (inherit from EventBase)
  - [x] RouteStatusUpdatedEvent (inherit from EventBase)
  - [x] RouteCapacityChangedEvent (inherit from EventBase)
- [x] Create relevant enums in SharedLibraries/MessageContracts/Enums/
  - [x] RouteStatus enum (Planned=1, Active=2, Completed=3, Cancelled=4, BookedPartial=5, BookedFull=6)

### 3. Data Model Implementation
- [x] Create Route entity with all required properties
  - [x] Basic properties (Id, TruckId, OwnerId, etc.)
  - [x] Geospatial properties:
    - [x] OriginPoint (NetTopologySuite.Geometries.Point)
    - [x] DestinationPoint (NetTopologySuite.Geometries.Point)
    - [x] ViaPoints (serialized as JSON string)
    - [x] GeometryPath (NetTopologySuite.Geometries.LineString)
  - [x] Time-related properties (DepartureTime, ArrivalTime, AvailableFrom, AvailableTo)
  - [x] Capacity properties:
    - [x] CapacityAvailableKg (decimal with precision)
    - [x] CapacityAvailableM3 (decimal with precision)
    - [x] TotalCapacityKg (decimal with precision)
    - [x] TotalCapacityM3 (decimal with precision)
  - [x] Status and metadata (Status, CreatedAt, UpdatedAt)
- [x] Create appropriate DTOs (Data Transfer Objects)
  - [x] RouteDto for responses
  - [x] CreateRouteRequest for route creation
  - [x] UpdateRouteRequest for route updates
  - [x] UpdateRouteCapacityRequest for capacity updates
  - [x] RouteFilterRequest for filtering routes in GET requests
- [x] Create RouteDbContext class
  - [x] Define DbSet<Route> Routes property
  - [x] Override OnModelCreating to configure entity:
    - [x] Set up keys and indexes
    - [x] Configure required fields and max lengths
    - [x] Configure precision for decimal properties
    - [x] Configure JSON serialization for ViaPoints
    - [x] Configure specific PostGIS column types
- [ ] Create initial migration

### 4. Service Layer Implementation
- [ ] Create Interface for Route Service
  - [ ] IRouteService with CRUD operations
- [ ] Implement Route Service
  - [ ] Create RouteService class implementing IRouteService
  - [ ] Implement validation logic
  - [ ] Implement CRUD operations
  - [ ] Integrate with EventPublisher to publish events
- [ ] Create GeospatialService for handling spatial calculations
  - [ ] Implement methods for calculating distances
  - [ ] Implement methods for validating geospatial data
  - [ ] Implement methods for converting between coordinate formats
- [ ] Implement route distance and duration calculation
  - [ ] Using PostGIS functions or
  - [ ] Integration with Mapbox Directions API via HTTP client

### 5. API Endpoints Implementation
- [ ] Create RouteController with the following endpoints:
  - [ ] POST /api/routes - Publish a new route
    - [ ] Validate input
    - [ ] Check authorization (must be a Truck Owner)
    - [ ] Verify truck ownership
    - [ ] Call RouteService to create the route
    - [ ] Return 201 Created with route details
  - [ ] GET /api/routes - List routes with filtering options
    - [ ] Implement filtering by owner, status, return leg, etc.
    - [ ] Implement pagination
    - [ ] Return 200 OK with list of routes
  - [ ] GET /api/routes/{id} - Get details for a specific route
    - [ ] Validate route exists
    - [ ] Check authorization
    - [ ] Return 200 OK with route details
  - [ ] PUT /api/routes/{id} - Modify an existing route
    - [ ] Validate input
    - [ ] Check authorization
    - [ ] Validate route exists and belongs to user
    - [ ] Call RouteService to update the route
    - [ ] Return 200 OK with updated route
  - [ ] DELETE /api/routes/{id} - Deactivate or cancel a route
    - [ ] Check authorization
    - [ ] Validate route exists and belongs to user
    - [ ] Call RouteService to deactivate the route
    - [ ] Return 204 No Content on success
  - [ ] PUT /api/routes/{id}/capacity - Update capacity after booking
    - [ ] Validate input
    - [ ] Check authorization (could be internal service call)
    - [ ] Call RouteService to update capacity
    - [ ] Return 200 OK with updated capacity info
- [ ] Implement ServiceDiscoveryController similar to TruckService
  - [ ] GET /api/servicediscovery - Return service details for discovery

### 6. Event Handling Implementation
- [ ] Implement EventPublisher similar to TruckService
  - [ ] Create IEventPublisher interface with methods for each event type
  - [ ] Create EventPublisher class that implements IEventPublisher
  - [ ] Implement methods for publishing each event type with logging
- [ ] Implement event consumers
  - [ ] Create RouteEventConsumers class
  - [ ] Implement consumers for BookingConfirmed and BookingCancelled events
  - [ ] Add logic to update route capacity when bookings change
- [ ] Configure MassTransit and RabbitMQ in Program.cs
  - [ ] Set up MassTransit with RabbitMQ
  - [ ] Register event consumers
  - [ ] Configure retry policies

### 7. Integration with Other Services
- [ ] Set up HTTP client for Truck Service integration
  - [ ] Configure named HTTP client in Program.cs
  - [ ] Implement TruckServiceClient to fetch truck capacity information
  - [ ] Add retry and circuit breaker policies
- [ ] Set up authentication and authorization
  - [ ] Configure JWT authentication middleware
  - [ ] Implement authorization policies for different operations
  - [ ] Add user context accessor for getting current user claims

### 8. API Documentation
- [ ] Configure Swagger/OpenAPI
  - [ ] Configure Swagger in Program.cs
  - [ ] Add XML comments to controllers and DTOs
  - [ ] Create examples for requests and responses
  - [ ] Add authentication to Swagger UI
- [ ] Create API documentation for all endpoints
  - [ ] Include request/response examples
  - [ ] Document error responses

### 9. Resilience Implementation
- [ ] Implement retry logic for external service calls
  - [ ] Configure Polly policies for HTTP requests
  - [ ] Apply retry policies to Truck Service calls
- [ ] Add circuit breaker patterns for external dependencies
  - [ ] Configure circuit breaker for Truck Service
  - [ ] Configure circuit breaker for event publishing
- [ ] Create fallback mechanisms for critical operations
  - [ ] Implement caching for truck information

### 10. Observability
- [ ] Configure logging
  - [ ] Set up Serilog with appropriate sinks
  - [ ] Create middleware for request/response logging
  - [ ] Configure log levels for different environments
  - [ ] Add correlation IDs for cross-service tracing
- [ ] Set up health checks
  - [ ] Implement health check for database connectivity
  - [ ] Implement health check for RabbitMQ connectivity
  - [ ] Implement health check for external dependencies
  - [ ] Create health check endpoint

### 11. Testing
- [ ] Create unit tests for services
  - [ ] RouteService tests
  - [ ] GeospatialService tests
  - [ ] EventPublisher tests
  - [ ] Controller tests
- [ ] Create integration tests for database operations
  - [ ] Test RouteDbContext with in-memory database
  - [ ] Test geospatial functions
- [ ] Create API tests for endpoints
  - [ ] Test CRUD operations
  - [ ] Test filtering and pagination
  - [ ] Test error cases

### 12. Deployment
- [ ] Create Dockerfile for the service
  - [ ] Multi-stage build for optimal image size
  - [ ] Include healthcheck definition
- [ ] Update docker-compose file to include Route Service
- [ ] Add PostgreSQL with PostGIS to the infrastructure
- [ ] Configure CI/CD pipeline for the service

## Dependencies
- Truck Service: For fetching truck capacity information
- User Service: For authentication and authorization
- API Gateway: For routing external requests
- PostgreSQL with PostGIS: For storing route data with geospatial features
- RabbitMQ: For event-based communication
- MessageContracts: Shared library for event definitions
- ServiceDiscovery: Shared library for service registration and discovery

## Outcomes
- Truck owners can publish planned routes
- Routes stored with proper geospatial data
- Route capacity is tracked and updated when bookings are made
- Events are published to notify other services about route changes
- Route data is available for the Matching Service to find potential matches with shipments 