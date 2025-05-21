using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ServiceDiscovery
{    /// <summary>
    /// Configuration-based service registry implementation
    /// </summary>
    public class ConfigurationServiceRegistry : IServiceRegistry
    {
        private readonly ConcurrentDictionary<string, ServiceInfo> _serviceDetails = new();
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ConfigurationServiceRegistry> _logger;

        public ConfigurationServiceRegistry(
            IConfiguration configuration, 
            HttpClient httpClient,
            ILogger<ConfigurationServiceRegistry> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Load services from configuration
            LoadServicesFromConfiguration();
        }        private void LoadServicesFromConfiguration()
        {
            var servicesSection = _configuration.GetSection("ServiceRegistry:Services");
            if (servicesSection.Exists())
            {
                foreach (var serviceConfig in servicesSection.GetChildren())
                {
                    var serviceName = serviceConfig.Key;
                    var serviceUrl = serviceConfig.Value;
                    
                    if (!string.IsNullOrEmpty(serviceUrl))
                    {
                        _serviceDetails.TryAdd(serviceName, new ServiceInfo 
                        {
                            Name = serviceName,
                            BaseUrl = serviceUrl,
                            HealthStatus = ServiceHealthStatus.Unknown,
                            LastRegistered = DateTime.UtcNow,
                            LastHealthCheck = null
                        });
                        
                        _logger.LogInformation("Loaded service configuration: {ServiceName} at {ServiceUrl}", 
                            serviceName, serviceUrl);
                    }
                }
            }
        }        /// <inheritdoc />
        public string GetServiceBaseUrl(string serviceName)
        {
            if (_serviceDetails.TryGetValue(serviceName, out var serviceInfo))
            {
                return serviceInfo.BaseUrl;
            }

            // Try to get from configuration directly if not found in dictionary
            var configuredUrl = _configuration[$"ServiceRegistry:Services:{serviceName}"];
            if (!string.IsNullOrEmpty(configuredUrl))
            {
                _serviceDetails.TryAdd(serviceName, new ServiceInfo
                {
                    Name = serviceName,
                    BaseUrl = configuredUrl,
                    HealthStatus = ServiceHealthStatus.Unknown,
                    LastRegistered = DateTime.UtcNow,
                    LastHealthCheck = null
                });
                return configuredUrl;
            }

            // Fallback to legacy configuration if available
            var legacyUrl = _configuration[$"ServiceUrls:{serviceName}"];
            if (!string.IsNullOrEmpty(legacyUrl))
            {
                _logger.LogWarning("Using legacy ServiceUrls configuration for {ServiceName}", serviceName);
                _serviceDetails.TryAdd(serviceName, new ServiceInfo
                {
                    Name = serviceName,
                    BaseUrl = legacyUrl,
                    HealthStatus = ServiceHealthStatus.Unknown,
                    LastRegistered = DateTime.UtcNow,
                    LastHealthCheck = null
                });
                return legacyUrl;
            }

            _logger.LogWarning("Service not found in registry: {ServiceName}", serviceName);
            throw new KeyNotFoundException($"Service '{serviceName}' not found in the registry");
        }        /// <inheritdoc />
        public async Task<bool> IsServiceAvailableAsync(string serviceName)
        {
            try
            {
                if (_serviceDetails.TryGetValue(serviceName, out var serviceInfo))
                {
                    var healthEndpoint = $"{serviceInfo.BaseUrl.TrimEnd('/')}/health";
                    var response = await _httpClient.GetAsync(healthEndpoint);
                    
                    // Update the health status
                    serviceInfo.LastHealthCheck = DateTime.UtcNow;
                    
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Service {ServiceName} is available", serviceName);
                        serviceInfo.HealthStatus = ServiceHealthStatus.Healthy;
                        return true;
                    }
                    
                    _logger.LogWarning("Service {ServiceName} health check failed with status: {Status}", 
                        serviceName, response.StatusCode);
                    serviceInfo.HealthStatus = ServiceHealthStatus.Unhealthy;
                    return false;
                }
                
                _logger.LogWarning("Cannot check availability - service not found in registry: {ServiceName}", 
                    serviceName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking service availability for {ServiceName}", serviceName);
                
                // Update health status to unhealthy if we have the service in our registry
                if (_serviceDetails.TryGetValue(serviceName, out var serviceInfo))
                {
                    serviceInfo.LastHealthCheck = DateTime.UtcNow;
                    serviceInfo.HealthStatus = ServiceHealthStatus.Unhealthy;
                }
                
                return false;
            }
        }        /// <inheritdoc />
        public void RegisterService(string serviceName, string serviceBaseUrl, ServiceHealthStatus healthStatus = ServiceHealthStatus.Healthy)
        {
            if (string.IsNullOrEmpty(serviceName))
            {
                throw new ArgumentNullException(nameof(serviceName));
            }
            
            if (string.IsNullOrEmpty(serviceBaseUrl))
            {
                throw new ArgumentNullException(nameof(serviceBaseUrl));
            }

            var serviceInfo = new ServiceInfo
            {
                Name = serviceName,
                BaseUrl = serviceBaseUrl,
                HealthStatus = healthStatus,
                LastRegistered = DateTime.UtcNow,
                LastHealthCheck = healthStatus != ServiceHealthStatus.Unknown ? DateTime.UtcNow : null
            };

            _serviceDetails.AddOrUpdate(serviceName, serviceInfo, (_, _) => serviceInfo);
            _logger.LogInformation("Service registered: {ServiceName} at {ServiceUrl} with status {Status}", 
                serviceName, serviceBaseUrl, healthStatus);
        }
        
        /// <inheritdoc />
        public void UpdateServiceHealth(string serviceName, ServiceHealthStatus healthStatus)
        {
            if (string.IsNullOrEmpty(serviceName))
            {
                throw new ArgumentNullException(nameof(serviceName));
            }
            
            if (_serviceDetails.TryGetValue(serviceName, out var serviceInfo))
            {
                serviceInfo.HealthStatus = healthStatus;
                serviceInfo.LastHealthCheck = DateTime.UtcNow;
                _logger.LogInformation("Updated service health: {ServiceName} to {Status}", 
                    serviceName, healthStatus);
            }
            else
            {
                _logger.LogWarning("Cannot update health - service not found in registry: {ServiceName}", 
                    serviceName);
                throw new KeyNotFoundException($"Service '{serviceName}' not found in the registry");
            }
        }        /// <inheritdoc />
        public IDictionary<string, string> GetAllServices()
        {
            return _serviceDetails.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value.BaseUrl);
        }
        
        /// <inheritdoc />
        public IEnumerable<ServiceInfo> GetAllServiceDetails()
        {
            return _serviceDetails.Values.ToList();
        }
    }
}
