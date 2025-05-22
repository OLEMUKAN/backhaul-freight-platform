using Microsoft.Extensions.Logging;
using RouteService.API.Services.Interfaces;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace RouteService.API.Services
{
    public class TruckServiceClient : ITruckServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TruckServiceClient> _logger;

        public TruckServiceClient(IHttpClientFactory httpClientFactory, ILogger<TruckServiceClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("TruckService");
            _logger = logger;
        }

        public async Task<bool> VerifyTruckOwnershipAsync(Guid truckId, Guid ownerId)
        {
            var requestUrl = $"/api/trucks/{truckId}/owner/{ownerId}";
            try
            {
                var response = await _httpClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Truck ownership verification failed: Truck {TruckId} for owner {OwnerId} not found (404).", truckId, ownerId);
                    return false;
                }
                
                _logger.LogError("Truck ownership verification failed for Truck {TruckId}, Owner {OwnerId}. Status code: {StatusCode}. Response: {Response}", 
                    truckId, ownerId, response.StatusCode, await response.Content.ReadAsStringAsync());
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception during truck ownership verification for Truck {TruckId}, Owner {OwnerId}.", truckId, ownerId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception during truck ownership verification for Truck {TruckId}, Owner {OwnerId}.", truckId, ownerId);
                return false;
            }
        }

        public async Task<(decimal CapacityKg, decimal? CapacityM3)> GetTruckCapacityAsync(Guid truckId)
        {
            var requestUrl = $"/api/trucks/{truckId}/capacity"; 
            try
            {
                var response = await _httpClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    if (response.Content == null)
                    {
                        _logger.LogWarning("GetTruckCapacityAsync for Truck {TruckId} returned successful status code but content was null.", truckId);
                        return (0, null);
                    }
                    try
                    {
                        var capacity = await response.Content.ReadFromJsonAsync<TruckCapacityDto>();
                        if (capacity != null)
                        {
                            return (capacity.CapacityKg, capacity.CapacityM3);
                        }
                        _logger.LogWarning("GetTruckCapacityAsync for Truck {TruckId} resulted in null after deserialization.", truckId);
                        return (0,null);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "JSON deserialization error during GetTruckCapacityAsync for Truck {TruckId}.", truckId);
                        return (0, null);
                    }
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("GetTruckCapacityAsync failed: Truck {TruckId} not found (404).", truckId);
                    return (0, null);
                }

                _logger.LogError("GetTruckCapacityAsync failed for Truck {TruckId}. Status code: {StatusCode}. Response: {Response}", 
                    truckId, response.StatusCode, await response.Content.ReadAsStringAsync());
                return (0, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception during GetTruckCapacityAsync for Truck {TruckId}.", truckId);
                return (0, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception during GetTruckCapacityAsync for Truck {TruckId}.", truckId);
                return (0, null);
            }
        }

        // DTO for deserializing capacity, property names match expected JSON (case-insensitive by default with System.Text.Json)
        private class TruckCapacityDto
        {
            public decimal CapacityKg { get; set; }
            public decimal? CapacityM3 { get; set; }
        }
    }
}
