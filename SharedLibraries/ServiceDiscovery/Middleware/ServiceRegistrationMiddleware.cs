using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDiscovery;
using System;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceDiscovery.Middleware
{
    /// <summary>
    /// Middleware that registers the current service with the service registry
    /// </summary>
    public class ServiceRegistrationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _serviceName;
        private readonly ILogger<ServiceRegistrationMiddleware> _logger;

        public ServiceRegistrationMiddleware(
            RequestDelegate next, 
            string serviceName,
            ILogger<ServiceRegistrationMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }        public async Task InvokeAsync(HttpContext context, IServiceRegistry serviceRegistry, IConfiguration configuration)
        {
            // Register the current service with the registry on first request
            try
            {
                string baseUrl = GetServiceBaseUrl(configuration);
                  // Check if health check is available and service is healthy
                ServiceHealthStatus healthStatus = ServiceHealthStatus.Healthy;
                try
                {
                    // Only check health if this is not the health check endpoint itself
                    if (!context.Request.Path.StartsWithSegments("/health"))
                    {
                        var healthCheckResult = await CheckServiceHealthAsync(baseUrl, TimeSpan.FromSeconds(2));
                        healthStatus = healthCheckResult ? ServiceHealthStatus.Healthy : ServiceHealthStatus.Degraded;
                        
                        if (!healthCheckResult)
                        {
                            _logger.LogWarning("Service {ServiceName} health check failed, registering with degraded status", _serviceName);
                        }
                    }
                }
                catch (Exception healthEx)
                {
                    _logger.LogWarning(healthEx, "Unable to check health for service {ServiceName}, registering with unknown status", _serviceName);
                    healthStatus = ServiceHealthStatus.Unknown;
                }
                
                // Register the service with the determined health status
                serviceRegistry.RegisterService(_serviceName, baseUrl, healthStatus);
                _logger.LogInformation("Service {ServiceName} registered at {BaseUrl} (Health: {HealthStatus})", 
                    _serviceName, baseUrl, healthStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering service {ServiceName} with registry", _serviceName);
            }

            await _next(context);
        }
        
        private async Task<bool> CheckServiceHealthAsync(string baseUrl, TimeSpan timeout)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = timeout };
                var healthEndpoint = $"{baseUrl.TrimEnd('/')}/health";
                
                var response = await httpClient.GetAsync(healthEndpoint);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private string GetServiceBaseUrl(IConfiguration configuration)
        {
            // First try to get from Kestrel configuration
            var httpsUrl = configuration["Kestrel:Endpoints:Https:Url"];
            if (!string.IsNullOrEmpty(httpsUrl))
            {
                return httpsUrl;
            }
            
            var httpUrl = configuration["Kestrel:Endpoints:Http:Url"];
            if (!string.IsNullOrEmpty(httpUrl))
            {
                return httpUrl;
            }

            // Fallback to configuration or default URL
            var configuredUrl = configuration[$"ServiceRegistry:Services:{_serviceName}"];
            if (!string.IsNullOrEmpty(configuredUrl))
            {
                return configuredUrl;
            }

            // Last resort: use a default URL pattern
            _logger.LogWarning("No explicit URL configuration found for service {ServiceName}, using default", _serviceName);
            return $"https://localhost:5000";
        }
    }

    /// <summary>
    /// Extension methods to use the service registration middleware
    /// </summary>
    public static class ServiceRegistrationMiddlewareExtensions
    {
        /// <summary>
        /// Adds middleware to register the current service with the service registry
        /// </summary>
        /// <param name="builder">The application builder</param>
        /// <param name="serviceName">The name of the current service</param>
        /// <returns>The application builder</returns>
        public static IApplicationBuilder UseServiceRegistration(
            this IApplicationBuilder builder, 
            string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
            {
                throw new ArgumentNullException(nameof(serviceName));
            }

            return builder.UseMiddleware<ServiceRegistrationMiddleware>(serviceName);
        }
    }
}
