using Microsoft.Extensions.Logging;
using RouteService.API.Services.Interfaces;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
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

        public async Task<bool> VerifyTruckOwnershipAsync(Guid truckId, Guid ownerId, CancellationToken cancellationToken = default)
        {
            var requestUrl = $"/api/trucks/{truckId}/owner/{ownerId}";
            try
            {
                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    if (response.Content == null)
                    {
                        _logger.LogWarning("Truck ownership verification for Truck {TruckId}, Owner {OwnerId} returned successful status code but content was null.", truckId, ownerId);
                        return false;
                    }
                    try
                    {
                        var verificationResponse = await response.Content.ReadFromJsonAsync<TruckOwnershipVerificationResponse>(cancellationToken: cancellationToken);
                        if (verificationResponse != null)
                        {
                            if (verificationResponse.IsOwner)
                            {
                                return true;
                            }
                            else
                            {
                                _logger.LogWarning("Truck ownership verification for Truck {TruckId}, Owner {OwnerId} returned IsOwner=false.", truckId, ownerId);
                                return false;
                            }
                        }
                        _logger.LogWarning("Truck ownership verification for Truck {TruckId}, Owner {OwnerId} resulted in null after deserialization.", truckId, ownerId);
                        return false;
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "JSON deserialization error during truck ownership verification for Truck {TruckId}, Owner {OwnerId}. Response: {Response}", 
                            truckId, ownerId, await response.Content.ReadAsStringAsync(cancellationToken));
                        return false;
                    }
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Truck ownership verification failed: Truck {TruckId} for owner {OwnerId} not found (404).", truckId, ownerId);
                    return false;
                }
                
                _logger.LogError("Truck ownership verification failed for Truck {TruckId}, Owner {OwnerId}. Status code: {StatusCode}. Response: {Response}", 
                    truckId, ownerId, response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception during truck ownership verification for Truck {TruckId}, Owner {OwnerId}.", truckId, ownerId);
                return false;
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(ex, "Truck ownership verification cancelled for Truck {TruckId}, Owner {OwnerId}.", truckId, ownerId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception during truck ownership verification for Truck {TruckId}, Owner {OwnerId}.", truckId, ownerId);
                return false;
            }
        }

        public async Task<(decimal CapacityKg, decimal? CapacityM3)> GetTruckCapacityAsync(Guid truckId, CancellationToken cancellationToken = default)
        {
            var requestUrl = $"/api/trucks/{truckId}/capacity"; 
            try
            {
                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    if (response.Content == null)
                    {
                        _logger.LogWarning("GetTruckCapacityAsync for Truck {TruckId} returned successful status code but content was null.", truckId);
                        return (0, null);
                    }
                    try
                    {
                        var capacity = await response.Content.ReadFromJsonAsync<TruckCapacityDto>(cancellationToken: cancellationToken);
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
                    truckId, response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
                return (0, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception during GetTruckCapacityAsync for Truck {TruckId}.", truckId);
                return (0, null);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(ex, "GetTruckCapacityAsync cancelled for Truck {TruckId}.", truckId);
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

        private class TruckOwnershipVerificationResponse
        {
            public bool IsOwner { get; set; }
        }
    }
}
