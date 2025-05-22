using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("gateway")]
    public class GatewayInfoController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GatewayInfoController> _logger;

        public GatewayInfoController(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<GatewayInfoController> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("info")]
        public IActionResult GetGatewayInfo()
        {
            _logger.LogInformation("Gateway info requested");

            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version?.ToString() ?? "Unknown";
            
            return Ok(new 
            {
                Name = "Backhaul Platform API Gateway",
                Version = assemblyVersion,
                Environment = _environment.EnvironmentName,
                BaseUrl = _configuration["GlobalConfiguration:BaseUrl"] ?? "https://localhost:7200",
                Status = "Running",
                Timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("routes")]
        public IActionResult GetAvailableRoutes()
        {
            _logger.LogInformation("Gateway routes info requested");

            // This is a simplified version just to show the concept
            // In a real system, you might want to extract route information from Ocelot config
            var routes = new List<object>
            {
                new { 
                    Path = "/api/users/**", 
                    Service = "UserService",
                    Methods = new[] { "GET", "POST", "PUT", "DELETE" },
                    RequiresAuth = true
                },
                new { 
                    Path = "/api/users/register", 
                    Service = "UserService",
                    Methods = new[] { "POST" },
                    RequiresAuth = false
                },
                new { 
                    Path = "/api/users/login", 
                    Service = "UserService",
                    Methods = new[] { "POST" },
                    RequiresAuth = false
                },
                new { 
                    Path = "/api/trucks/**", 
                    Service = "TruckService",
                    Methods = new[] { "GET", "POST", "PUT", "DELETE" },
                    RequiresAuth = true
                },
                new {
                    Path = "/api/truckowners/{userId}",
                    Service = "Aggregated (UserService + TruckService)",
                    Methods = new[] { "GET" },
                    RequiresAuth = true,
                    IsAggregated = true
                },
                new {
                    Path = "/api/routes/**",
                    Service = "RouteService",
                    Methods = new[] { "GET", "POST", "PUT", "DELETE" },
                    RequiresAuth = true
                },
                new {
                    Path = "/api/routes/{id}/capacity",
                    Service = "RouteService",
                    Methods = new[] { "PUT" },
                    RequiresAuth = true
                }
            };

            return Ok(new { 
                Routes = routes,
                Count = routes.Count
            });
        }
    }
}
