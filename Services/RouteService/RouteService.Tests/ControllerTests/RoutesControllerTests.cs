using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RouteService.API.Services.Interfaces;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using RouteService.API.Dtos.Routes; // Required for DTOs
using MessageContracts.Enums; // Required for RouteStatus
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration; // For IConfiguration

// Alias to avoid conflict with the namespace
using ApiProgram = RouteService.API.Program;


namespace RouteService.Tests.ControllerTests
{
    public class RoutesControllerTests : IClassFixture<WebApplicationFactory<ApiProgram>>
    {
        private readonly WebApplicationFactory<ApiProgram> _factory;
        private readonly Mock<IRouteService> _mockRouteService;
        private readonly HttpClient _client;

        // Sample Guids for testing
        private readonly Guid _sampleRouteId = Guid.NewGuid();
        private readonly Guid _sampleOwnerId = Guid.NewGuid();
        private readonly Guid _sampleTruckId = Guid.NewGuid();
        private readonly Guid _sampleAdminId = Guid.NewGuid();


        public RoutesControllerTests(WebApplicationFactory<ApiProgram> factory)
        {
            _mockRouteService = new Mock<IRouteService>();
            
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(_mockRouteService.Object);
                    
                    // Remove the original IConfiguration source if it relies on files not present in test
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConfiguration));
                    if (descriptor != null) { /* services.Remove(descriptor); */ } // Be careful removing this, might break other things.
                                                                                // Better to provide a specific test configuration if needed.

                    // Add a test configuration if necessary, e.g., for JWT
                    var testConfig = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string> {
                            {"JWT:Secret", "SuperSecretTestKey1234567890123456"},
                            {"JWT:ValidIssuer", "test-issuer"},
                            {"JWT:ValidAudience", "test-audience"},
                            {"RabbitMQ:Host", "testhost"}, // Mocked or not used by controller tests directly
                            {"ExternalServices:TruckService", "http://localhost:9999"} // Mocked or not used
                        })
                        .Build();
                    services.AddSingleton<IConfiguration>(testConfig);
                });
            });
            _client = _factory.CreateClient();
        }

        private string GenerateJwtToken(Guid userId, string role)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            // Use the secret from the test configuration
            var key = Encoding.ASCII.GetBytes(_factory.Services.GetRequiredService<IConfiguration>()["JWT:Secret"]);
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            if (!string.IsNullOrEmpty(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = _factory.Services.GetRequiredService<IConfiguration>()["JWT:ValidIssuer"],
                Audience = _factory.Services.GetRequiredService<IConfiguration>()["JWT:ValidAudience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // --- POST /api/routes ---
        [Fact]
        public async Task CreateRoute_ValidRequest_ReturnsCreatedAtRoute()
        {
            // Arrange
            var request = new CreateRouteRequest { TruckId = _sampleTruckId, OriginCoordinates = new[] { 1.0, 1.0 }, DestinationCoordinates = new[] { 2.0, 2.0 }, ScheduledDeparture = DateTimeOffset.UtcNow.AddHours(1), ScheduledArrival = DateTimeOffset.UtcNow.AddHours(2) };
            var expectedDto = new RouteDto { Id = _sampleRouteId, OwnerId = _sampleOwnerId, TruckId = _sampleTruckId, Status = RouteStatus.Planned };
            
            _mockRouteService.Setup(s => s.CreateRouteAsync(It.IsAny<CreateRouteRequest>(), _sampleOwnerId))
                .ReturnsAsync(expectedDto);

            var token = GenerateJwtToken(_sampleOwnerId, "TruckOwner");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PostAsJsonAsync("/api/routes", request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdRoute = await response.Content.ReadFromJsonAsync<RouteDto>();
            Assert.NotNull(createdRoute);
            Assert.Equal(_sampleRouteId, createdRoute.Id);
            Assert.Equal($"/api/routes/{_sampleRouteId}", response.Headers.Location?.OriginalString);
            _mockRouteService.Verify(s => s.CreateRouteAsync(It.IsAny<CreateRouteRequest>(), _sampleOwnerId), Times.Once);
        }

        [Fact]
        public async Task CreateRoute_ServiceThrowsArgumentException_ReturnsBadRequest()
        {
            var request = new CreateRouteRequest { TruckId = _sampleTruckId }; // Potentially invalid
            _mockRouteService.Setup(s => s.CreateRouteAsync(It.IsAny<CreateRouteRequest>(), _sampleOwnerId))
                .ThrowsAsync(new ArgumentException("Test argument exception"));
            
            var token = GenerateJwtToken(_sampleOwnerId, "TruckOwner");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PostAsJsonAsync("/api/routes", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadAsStringAsync();
            Assert.Contains("Test argument exception", error);
        }
        
        [Fact]
        public async Task CreateRoute_NoToken_ReturnsUnauthorized()
        {
            var request = new CreateRouteRequest { TruckId = _sampleTruckId };
            _client.DefaultRequestHeaders.Authorization = null; // Ensure no auth header

            var response = await _client.PostAsJsonAsync("/api/routes", request);
            
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateRoute_IncorrectRole_ReturnsForbidden()
        {
            var request = new CreateRouteRequest { TruckId = _sampleTruckId };
            var token = GenerateJwtToken(_sampleOwnerId, "NotATruckOwner"); // Wrong role
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PostAsJsonAsync("/api/routes", request);
            
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // --- GET /api/routes/{id} ---
        [Fact]
        public async Task GetRouteById_RouteExists_ReturnsOkWithRouteDto()
        {
            var expectedDto = new RouteDto { Id = _sampleRouteId, OwnerId = _sampleOwnerId };
            _mockRouteService.Setup(s => s.GetRouteByIdAsync(_sampleRouteId)).ReturnsAsync(expectedDto);

            var response = await _client.GetAsync($"/api/routes/{_sampleRouteId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var route = await response.Content.ReadFromJsonAsync<RouteDto>();
            Assert.NotNull(route);
            Assert.Equal(_sampleRouteId, route.Id);
        }

        [Fact]
        public async Task GetRouteById_RouteDoesNotExist_ReturnsNotFound()
        {
            _mockRouteService.Setup(s => s.GetRouteByIdAsync(It.IsAny<Guid>())).ReturnsAsync((RouteDto)null);

            var response = await _client.GetAsync($"/api/routes/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // --- GET /api/routes ---
        [Fact]
        public async Task GetRoutes_ReturnsOkWithListOfRoutes()
        {
            var routesList = new List<RouteDto> { new RouteDto { Id = Guid.NewGuid() }, new RouteDto { Id = Guid.NewGuid() } };
            _mockRouteService.Setup(s => s.GetRoutesAsync(It.IsAny<RouteFilterRequest>())).ReturnsAsync(routesList);

            var response = await _client.GetAsync("/api/routes?Page=1&PageSize=10"); // Example query

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var routes = await response.Content.ReadFromJsonAsync<List<RouteDto>>();
            Assert.NotNull(routes);
            Assert.Equal(2, routes.Count);
        }
        
        // --- PUT /api/routes/{id} ---
        [Fact]
        public async Task UpdateRoute_ValidRequest_ReturnsOkWithUpdatedDto()
        {
            var updateRequest = new UpdateRouteRequest { Notes = "Updated note" };
            var updatedDto = new RouteDto { Id = _sampleRouteId, OwnerId = _sampleOwnerId, Notes = "Updated note" };

            _mockRouteService.Setup(s => s.UpdateRouteAsync(_sampleRouteId, It.IsAny<UpdateRouteRequest>(), _sampleOwnerId))
                .ReturnsAsync(updatedDto);

            var token = GenerateJwtToken(_sampleOwnerId, "TruckOwner");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PutAsJsonAsync($"/api/routes/{_sampleRouteId}", updateRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var route = await response.Content.ReadFromJsonAsync<RouteDto>();
            Assert.NotNull(route);
            Assert.Equal("Updated note", route.Notes);
        }

        [Fact]
        public async Task UpdateRoute_ServiceReturnsNull_ReturnsNotFound()
        {
            var updateRequest = new UpdateRouteRequest { Notes = "Updated note" };
            _mockRouteService.Setup(s => s.UpdateRouteAsync(It.IsAny<Guid>(), It.IsAny<UpdateRouteRequest>(), _sampleOwnerId))
                .ReturnsAsync((RouteDto)null);

            var token = GenerateJwtToken(_sampleOwnerId, "TruckOwner");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PutAsJsonAsync($"/api/routes/{Guid.NewGuid()}", updateRequest);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        
        // --- DELETE /api/routes/{id} ---
        [Fact]
        public async Task CancelRoute_ValidRequest_ReturnsNoContent()
        {
            _mockRouteService.Setup(s => s.CancelRouteAsync(_sampleRouteId, _sampleOwnerId)).ReturnsAsync(true);
            var token = GenerateJwtToken(_sampleOwnerId, "TruckOwner");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.DeleteAsync($"/api/routes/{_sampleRouteId}");
            
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task CancelRoute_ServiceReturnsFalse_ReturnsNotFound()
        {
            _mockRouteService.Setup(s => s.CancelRouteAsync(It.IsAny<Guid>(), _sampleOwnerId)).ReturnsAsync(false);
            var token = GenerateJwtToken(_sampleOwnerId, "TruckOwner");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.DeleteAsync($"/api/routes/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // --- PUT /api/routes/{id}/capacity ---
        [Fact]
        public async Task UpdateRouteCapacity_ValidRequest_ReturnsOkWithDto()
        {
            var capacityRequest = new UpdateRouteCapacityRequest { CapacityChangeKg = -100m };
            var updatedDto = new RouteDto { Id = _sampleRouteId, CapacityAvailableKg = 900m };

            _mockRouteService.Setup(s => s.UpdateRouteCapacityAsync(_sampleRouteId, It.IsAny<UpdateRouteCapacityRequest>()))
                .ReturnsAsync(updatedDto);

            var token = GenerateJwtToken(_sampleAdminId, "Admin"); // Using Admin role as per policy
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PutAsJsonAsync($"/api/routes/{_sampleRouteId}/capacity", capacityRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var route = await response.Content.ReadFromJsonAsync<RouteDto>();
            Assert.NotNull(route);
            Assert.Equal(900m, route.CapacityAvailableKg);
        }
        
        [Fact]
        public async Task UpdateRouteCapacity_NotAdmin_ReturnsForbidden()
        {
            var capacityRequest = new UpdateRouteCapacityRequest { CapacityChangeKg = -100m };
            var token = GenerateJwtToken(_sampleOwnerId, "TruckOwner"); // Not an Admin
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PutAsJsonAsync($"/api/routes/{_sampleRouteId}/capacity", capacityRequest);
            
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
