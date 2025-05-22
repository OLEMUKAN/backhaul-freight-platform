using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RouteService.API.Services.Interfaces;

namespace RouteService.API.Services
{
    /// <summary>
    /// Client for interacting with the Truck Service API
    /// </summary>
    public class TruckServiceClient : ITruckServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TruckServiceClient> _logger;
        
        public TruckServiceClient(IHttpClientFactory httpClientFactory, ILogger<TruckServiceClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("TruckService") 
                ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc />
        public async Task<bool> VerifyTruckOwnershipAsync(Guid truckId, Guid ownerId)
        {
            try
            {
                _logger.LogInformation("Verifying ownership of truck {TruckId} for owner {OwnerId}", truckId, ownerId);
                
                var response = await _httpClient.GetAsync($"api/trucks/{truckId}/verify-ownership/{ownerId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<bool>();
                    return result;
                }
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Truck {TruckId} or owner {OwnerId} not found", truckId, ownerId);
                    return false;
                }
                
                _logger.LogWarning("Failed to verify truck ownership. Status code: {StatusCode}", response.StatusCode);
                response.EnsureSuccessStatusCode(); // Throw exception for non-success codes other than NotFound
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying ownership of truck {TruckId} for owner {OwnerId}", truckId, ownerId);
                throw;
            }
        }
        
        /// <inheritdoc />
        public async Task<(decimal CapacityKg, decimal? CapacityM3)> GetTruckCapacityAsync(Guid truckId)
        {
            try
            {
                _logger.LogInformation("Getting capacity information for truck {TruckId}", truckId);
                
                var response = await _httpClient.GetAsync($"api/trucks/{truckId}/capacity");
                
                response.EnsureSuccessStatusCode();
                
                var capacity = await response.Content.ReadFromJsonAsync<TruckCapacityResponse>();
                
                if (capacity == null)
                {
                    _logger.LogWarning("Received null capacity for truck {TruckId}", truckId);
                    throw new InvalidOperationException($"Received null capacity from Truck Service for truck {truckId}");
                }
                
                _logger.LogInformation("Retrieved capacity for truck {TruckId}: {CapacityKg} kg, {CapacityM3} m³", 
                    truckId, capacity.CapacityKg, capacity.CapacityM3);
                    
                return (capacity.CapacityKg, capacity.CapacityM3);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting capacity for truck {TruckId}", truckId);
                throw;
            }
        }
        
        /// <summary>
        /// DTO for truck capacity response
        /// </summary>
        private class TruckCapacityResponse
        {
            public decimal CapacityKg { get; set; }
            public decimal? CapacityM3 { get; set; }
        }
    }
}
