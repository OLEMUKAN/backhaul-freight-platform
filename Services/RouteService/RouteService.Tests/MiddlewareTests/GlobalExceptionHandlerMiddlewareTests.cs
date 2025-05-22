using System.Net;
using System.Net.Http.Json; // For ReadFromJsonAsync (or use System.Text.Json.JsonSerializer)
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

// Alias to avoid conflict with the namespace for Program.cs
using ApiProgram = RouteService.API.Program; 

namespace RouteService.Tests.MiddlewareTests
{
    public class GlobalExceptionHandlerMiddlewareTests : IClassFixture<WebApplicationFactory<ApiProgram>> 
    {
        private readonly WebApplicationFactory<ApiProgram> _factory;

        public GlobalExceptionHandlerMiddlewareTests(WebApplicationFactory<ApiProgram> factory)
        {
            // Ensure the factory is configured to use the test environment
            // if you have specific appsettings.Test.json or similar.
            // The middleware should be registered in Program.cs for this to work.
            _factory = factory;
        }

        // Helper class to deserialize the standardized error response
        private class ErrorResponse 
        {
            public int StatusCode { get; set; }
            public string Message { get; set; }
        }

        [Theory]
        [InlineData("/api/exceptiontest/argument", HttpStatusCode.BadRequest, "Test ArgumentException")]
        [InlineData("/api/exceptiontest/validation", HttpStatusCode.BadRequest, "Test ValidationException")]
        [InlineData("/api/exceptiontest/unauthorized", HttpStatusCode.Unauthorized, "Unauthorized access.")]
        [InlineData("/api/exceptiontest/notfound", HttpStatusCode.NotFound, "Test KeyNotFoundException")]
        [InlineData("/api/exceptiontest/generic", HttpStatusCode.InternalServerError, "An internal server error occurred.")]
        [InlineData("/api/exceptiontest/operationcanceled", (HttpStatusCode)499, "Request was cancelled by the client.")]
        public async Task EndpointThrowsException_MiddlewareReturnsCorrectErrorResponse(string url, HttpStatusCode expectedStatusCode, string expectedMessage)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(url);

            // Assert
            Assert.Equal(expectedStatusCode, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var errorContent = await response.Content.ReadAsStringAsync();
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(
                errorContent, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true } // Ensure camelCase matching
            );
            
            Assert.NotNull(errorResponse);
            Assert.Equal((int)expectedStatusCode, errorResponse.StatusCode);
            Assert.Equal(expectedMessage, errorResponse.Message);
        }
    }
}
