using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ServiceDiscovery;

namespace UserService.API.Controllers
{
    // TODO: Tech Debt - This controller is duplicated in UserService.API.
    // Consider refactoring into a shared library or NuGet package if this functionality
    // needs to be consistently exposed across multiple services.
    // Alternatively, evaluate if these endpoints are necessary in individual services
    // if the API Gateway is the primary consumer of IServiceRegistry.
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceDiscoveryController : ControllerBase
    {
        private readonly IServiceRegistry _serviceRegistry;
        private readonly ILogger<ServiceDiscoveryController> _logger;

        public ServiceDiscoveryController(
            IServiceRegistry serviceRegistry,
            ILogger<ServiceDiscoveryController> logger)
        {
            _serviceRegistry = serviceRegistry;
            _logger = logger;
        }        /// <summary>
        /// Get all registered services (basic info)
        /// </summary>
        /// <returns>Dictionary of service names and URLs</returns>
        [HttpGet("services")]
        [AllowAnonymous] // Allow anonymous access for testing
        public ActionResult<IDictionary<string, string>> GetServices()
        {
            try
            {
                var services = _serviceRegistry.GetAllServices();
                return Ok(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting service registry information");
                return StatusCode(500, "Error retrieving service registry information");
            }
        }
        
        /// <summary>
        /// Get detailed information about all registered services
        /// </summary>
        /// <returns>Collection of detailed service information</returns>
        [HttpGet("services/details")]
        [AllowAnonymous] // Allow anonymous access for testing
        public ActionResult<IEnumerable<ServiceInfo>> GetServiceDetails()
        {
            try
            {
                var serviceDetails = _serviceRegistry.GetAllServiceDetails();
                return Ok(serviceDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed service registry information");
                return StatusCode(500, "Error retrieving detailed service registry information");
            }
        }        /// <summary>
        /// Check service health
        /// </summary>
        /// <param name="serviceName">Service name to check</param>
        /// <returns>Health status</returns>
        [HttpGet("check/{serviceName}")]
        [AllowAnonymous] // Allow anonymous access for testing
        public async Task<ActionResult<bool>> CheckServiceHealth(string serviceName)
        {
            try
            {
                // Check service availability and get details
                var isAvailable = await _serviceRegistry.IsServiceAvailableAsync(serviceName);
                var serviceDetails = _serviceRegistry.GetAllServiceDetails()
                    .FirstOrDefault(s => s.Name == serviceName);
                    
                return Ok(new { 
                    serviceName, 
                    isAvailable,
                    serviceDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking service {ServiceName} health", serviceName);
                return StatusCode(500, $"Error checking service health: {ex.Message}");
            }
        }
    }
}
