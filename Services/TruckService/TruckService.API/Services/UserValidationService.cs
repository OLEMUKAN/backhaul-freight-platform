using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using ServiceDiscovery;

namespace TruckService.API.Services
{    public class UserValidationService : IUserValidationService
    {
        private readonly ServiceHttpClientFactory _serviceHttpClientFactory;
        private readonly IServiceRegistry _serviceRegistry;
        private readonly ILogger<UserValidationService> _logger;
        private const string SERVICE_NAME = "UserService";

        public UserValidationService(
            ServiceHttpClientFactory serviceHttpClientFactory,
            IServiceRegistry serviceRegistry,
            ILogger<UserValidationService> logger)
        {
            _serviceHttpClientFactory = serviceHttpClientFactory ?? throw new ArgumentNullException(nameof(serviceHttpClientFactory));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }        public async Task<bool> ValidateUserExistsAsync(Guid userId)
        {
            try
            {
                // Use the resilient client factory to make the request
                var response = await _serviceHttpClientFactory.ExecuteWithResilienceAsync(
                    SERVICE_NAME, 
                    client => client.GetAsync($"api/users/validate/{userId}"));
                
                return response.IsSuccessStatusCode;
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning("Circuit breaker is open, user validation call prevented for user {UserId}", userId);
                // In case of circuit breaker open, we return true to avoid blocking operations
                // This is a fallback strategy but should be monitored
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user existence for ID {UserId}", userId);
                // In case of failure, we return true to avoid blocking operations
                // This is a fallback strategy but should be monitored
                return true;
            }
        }        public async Task<bool> ValidateUserIsActiveAsync(Guid userId, string requiredRole = "TruckOwner")
        {
            try
            {
                // Use the resilient client factory to make the request
                var response = await _serviceHttpClientFactory.ExecuteWithResilienceAsync(
                    SERVICE_NAME, 
                    client => client.GetAsync($"api/users/validate/{userId}/role/{requiredRole}"));
                
                return response.IsSuccessStatusCode;
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning("Circuit breaker is open, user role validation call prevented for user {UserId} with role {Role}", 
                    userId, requiredRole);
                // In case of circuit breaker open, we return true to avoid blocking operations
                // This is a fallback strategy but should be monitored
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user {UserId} has role {Role}", userId, requiredRole);
                // In case of failure, we return true to avoid blocking operations
                // This is a fallback strategy but should be monitored
                return true;
            }
        }public async Task<bool> CheckUserServiceHealthAsync()
        {
            try
            {
                // Use the service registry directly to check service health
                bool isAvailable = await _serviceRegistry.IsServiceAvailableAsync("UserService");
                
                if (isAvailable)
                {
                    _logger.LogInformation("User Service health check passed");
                    return true;
                }
                
                _logger.LogWarning("User Service health check failed");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking User Service health");
                return false;
            }
        }
    }
} 