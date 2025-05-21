using System;
using System.Threading.Tasks;

namespace ServiceDiscovery
{
    /// <summary>
    /// Represents the health status of a service
    /// </summary>
    public enum ServiceHealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy,
        Unknown
    }
    
    /// <summary>
    /// Represents information about a registered service
    /// </summary>
    public class ServiceInfo
    {
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public ServiceHealthStatus HealthStatus { get; set; }
        public DateTime LastRegistered { get; set; }
        public DateTime? LastHealthCheck { get; set; }
    }
    
    /// <summary>
    /// Interface for service registry functionality
    /// </summary>
    public interface IServiceRegistry
    {        /// <summary>
        /// Gets the base URL for a service
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <returns>The base URL for the service</returns>
        string GetServiceBaseUrl(string serviceName);

        /// <summary>
        /// Determines if a service is available
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <returns>True if the service is available, false otherwise</returns>
        Task<bool> IsServiceAvailableAsync(string serviceName);
        
        /// <summary>
        /// Registers a service with the registry
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <param name="serviceBaseUrl">The base URL for the service</param>
        /// <param name="healthStatus">The current health status of the service</param>
        void RegisterService(string serviceName, string serviceBaseUrl, ServiceHealthStatus healthStatus = ServiceHealthStatus.Healthy);
        
        /// <summary>
        /// Updates the health status of a service
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <param name="healthStatus">The current health status of the service</param>
        void UpdateServiceHealth(string serviceName, ServiceHealthStatus healthStatus);

        /// <summary>
        /// Gets all registered services
        /// </summary>
        /// <returns>A dictionary of service names and their base URLs</returns>
        IDictionary<string, string> GetAllServices();
        
        /// <summary>
        /// Gets detailed information about all registered services
        /// </summary>
        /// <returns>A collection of service information</returns>
        IEnumerable<ServiceInfo> GetAllServiceDetails();
    }
}
