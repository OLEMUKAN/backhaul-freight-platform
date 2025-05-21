using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/truckowners")]
    public class TruckOwnerController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TruckOwnerController> _logger;

        public TruckOwnerController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<TruckOwnerController> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("{userId}/data")]
        public async Task<IActionResult> GetTruckOwnerData(string userId)
        {
            try
            {
                // Get user data from the User service
                var userServiceUrl = _configuration["Services:UserService:BaseUrl"] ?? "https://localhost:2999";
                var userResponse = await _httpClient.GetAsync($"{userServiceUrl}/api/users/{userId}");
                
                if (!userResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get user data from user service. Status: {StatusCode}", userResponse.StatusCode);
                    return StatusCode((int)userResponse.StatusCode, 
                        $"User service returned status code {userResponse.StatusCode}");
                }
                
                var userData = await userResponse.Content.ReadAsStringAsync();
                var userJson = JsonDocument.Parse(userData).RootElement;
                
                // Get trucks data from the Truck service
                var truckServiceUrl = _configuration["Services:TruckService:BaseUrl"] ?? "https://localhost:7198";
                var trucksResponse = await _httpClient.GetAsync($"{truckServiceUrl}/api/trucks?ownerId={userId}");
                
                if (!trucksResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get trucks data from truck service. Status: {StatusCode}", trucksResponse.StatusCode);
                    return StatusCode((int)trucksResponse.StatusCode, 
                        $"Truck service returned status code {trucksResponse.StatusCode}");
                }
                
                var trucksData = await trucksResponse.Content.ReadAsStringAsync();
                var trucksJson = JsonDocument.Parse(trucksData).RootElement;
                
                // Create the aggregated response
                var result = new
                {
                    User = userJson,
                    Trucks = trucksJson
                };
                
                _logger.LogInformation("Successfully aggregated data for truck owner {UserId}", userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating data for truck owner {UserId}", userId);
                return StatusCode(500, "Internal server error while aggregating truck owner data");
            }
        }
    }
}
