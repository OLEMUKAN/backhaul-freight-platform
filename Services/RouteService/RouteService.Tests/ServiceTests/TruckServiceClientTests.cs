using Xunit;
using Moq;
using Moq.Protected;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RouteService.API.Services;
using RouteService.API.Services.Interfaces; // Required for ITruckServiceClient

namespace RouteService.Tests.ServiceTests
{
    // Helper class to mock HttpMessageHandler
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
        {
            _handlerFunc = handlerFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handlerFunc(request, cancellationToken);
        }
    }

    public class TruckServiceClientTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<TruckServiceClient>> _mockLogger;
        private ITruckServiceClient _truckServiceClient; // Use interface

        public TruckServiceClientTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<TruckServiceClient>>();
        }

        private void SetupHttpClient(HttpResponseMessage httpResponseMessage)
        {
            var mockHttpMessageHandler = new MockHttpMessageHandler(async (request, token) =>
            {
                // Store the request URI if needed for assertions later
                // For example, you might want to verify the exact URI called.
                // request.RequestUri can be inspected here.
                return await Task.FromResult(httpResponseMessage);
            });

            var httpClient = new HttpClient(mockHttpMessageHandler)
            {
                BaseAddress = new Uri("http://truckservice-api") // Base address for named client
            };
            
            _mockHttpClientFactory.Setup(_ => _.CreateClient("TruckService")).Returns(httpClient);
            _truckServiceClient = new TruckServiceClient(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        // Tests for VerifyTruckOwnershipAsync
        [Fact]
        public async Task VerifyTruckOwnershipAsync_SuccessfulResponse_IsOwnerTrue_ReturnsTrue()
        {
            var truckId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var jsonResponse = "{\"isOwner\": true}";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };
            SetupHttpClient(httpResponse);

            var result = await _truckServiceClient.VerifyTruckOwnershipAsync(truckId, ownerId, CancellationToken.None);

            Assert.True(result);
        }

        [Fact]
        public async Task VerifyTruckOwnershipAsync_SuccessfulResponse_IsOwnerFalse_ReturnsFalse()
        {
            var truckId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var jsonResponse = "{\"isOwner\": false}";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };
            SetupHttpClient(httpResponse);

            var result = await _truckServiceClient.VerifyTruckOwnershipAsync(truckId, ownerId, CancellationToken.None);

            Assert.False(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Truck ownership verification for Truck {truckId}, Owner {ownerId} returned IsOwner=false.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        
        [Fact]
        public async Task VerifyTruckOwnershipAsync_SuccessfulResponse_MalformedJson_ReturnsFalse()
        {
            var truckId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var malformedJsonResponse = "{\"isOwner\": true"; // Missing closing brace
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(malformedJsonResponse, System.Text.Encoding.UTF8, "application/json")
            };
            SetupHttpClient(httpResponse);

            var result = await _truckServiceClient.VerifyTruckOwnershipAsync(truckId, ownerId, CancellationToken.None);

            Assert.False(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error, // Changed to Error because JsonException is logged as Error
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("JSON deserialization error during truck ownership verification")),
                    It.IsAny<JsonException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        
        [Fact]
        public async Task VerifyTruckOwnershipAsync_SuccessfulResponse_NullContent_ReturnsFalse()
        {
            var truckId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = null }; // Null content
            SetupHttpClient(httpResponse);

            var result = await _truckServiceClient.VerifyTruckOwnershipAsync(truckId, ownerId, CancellationToken.None);

            Assert.False(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("returned successful status code but content was null")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }


        [Fact]
        public async Task VerifyTruckOwnershipAsync_NotFoundResponse_ReturnsFalse()
        {
            var truckId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
            SetupHttpClient(httpResponse);

            var result = await _truckServiceClient.VerifyTruckOwnershipAsync(truckId, ownerId, CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task VerifyTruckOwnershipAsync_HttpError_ReturnsFalse()
        {
            var truckId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            SetupHttpClient(httpResponse);

            var result = await _truckServiceClient.VerifyTruckOwnershipAsync(truckId, ownerId, CancellationToken.None);

            Assert.False(result);
            // Optionally verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Truck ownership verification failed")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        
        [Fact]
        public async Task VerifyTruckOwnershipAsync_HttpRequestException_ReturnsFalse()
        {
            var truckId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
        
            var mockHttpMessageHandler = new MockHttpMessageHandler(async (request, token) =>
            {
                throw new HttpRequestException("Simulated request exception");
            });
        
            var httpClient = new HttpClient(mockHttpMessageHandler) { BaseAddress = new Uri("http://truckservice-api") };
            _mockHttpClientFactory.Setup(_ => _.CreateClient("TruckService")).Returns(httpClient);
            _truckServiceClient = new TruckServiceClient(_mockHttpClientFactory.Object, _mockLogger.Object);

            var result = await _truckServiceClient.VerifyTruckOwnershipAsync(truckId, ownerId, CancellationToken.None);

            Assert.False(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("HTTP request exception during truck ownership verification")),
                    It.IsAny<HttpRequestException>(), // Verify the exception type
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        // Tests for GetTruckCapacityAsync
        [Fact]
        public async Task GetTruckCapacityAsync_SuccessfulResponse_ReturnsCapacities()
        {
            var truckId = Guid.NewGuid();
            var expectedCapacityKg = 10000m;
            var expectedCapacityM3 = 60.5m;
            var jsonContent = new StringContent(
                $"{{\"capacityKg\": {expectedCapacityKg}, \"capacityM3\": {expectedCapacityM3}}}", 
                System.Text.Encoding.UTF8, 
                "application/json"
            );
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = jsonContent };
            SetupHttpClient(httpResponse);

            var (capacityKg, capacityM3) = await _truckServiceClient.GetTruckCapacityAsync(truckId, CancellationToken.None);

            Assert.Equal(expectedCapacityKg, capacityKg);
            Assert.Equal(expectedCapacityM3, capacityM3);
        }
        
        [Fact]
        public async Task GetTruckCapacityAsync_SuccessfulResponse_CapacityM3Null_ReturnsCapacities()
        {
            var truckId = Guid.NewGuid();
            var expectedCapacityKg = 12000m;
            // CapacityM3 is explicitly null in the JSON
            var jsonContent = new StringContent(
                $"{{\"capacityKg\": {expectedCapacityKg}, \"capacityM3\": null}}",
                System.Text.Encoding.UTF8,
                "application/json"
            );
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = jsonContent };
            SetupHttpClient(httpResponse);

            var (capacityKg, capacityM3) = await _truckServiceClient.GetTruckCapacityAsync(truckId, CancellationToken.None);

            Assert.Equal(expectedCapacityKg, capacityKg);
            Assert.Null(capacityM3);
        }


        [Fact]
        public async Task GetTruckCapacityAsync_NotFoundResponse_ReturnsZeroAndNull()
        {
            var truckId = Guid.NewGuid();
            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
            SetupHttpClient(httpResponse);

            var (capacityKg, capacityM3) = await _truckServiceClient.GetTruckCapacityAsync(truckId, CancellationToken.None);

            Assert.Equal(0, capacityKg);
            Assert.Null(capacityM3);
             _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("GetTruckCapacityAsync failed: Truck") && v.ToString().Contains("not found (404)")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetTruckCapacityAsync_JsonDeserializationFailure_ReturnsZeroAndNull()
        {
            var truckId = Guid.NewGuid();
            var invalidJsonContent = new StringContent("{\"invalidJson\": true}", System.Text.Encoding.UTF8, "application/json");
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = invalidJsonContent };
            SetupHttpClient(httpResponse);

            var (capacityKg, capacityM3) = await _truckServiceClient.GetTruckCapacityAsync(truckId, CancellationToken.None);

            Assert.Equal(0, capacityKg);
            Assert.Null(capacityM3);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("JSON deserialization error during GetTruckCapacityAsync")),
                    It.IsAny<JsonException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        
        [Fact]
        public async Task GetTruckCapacityAsync_SuccessfulResponse_NullContent_ReturnsZeroAndNull()
        {
            var truckId = Guid.NewGuid();
            // HttpResponseMessage with OK status but Content is null
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = null };
            SetupHttpClient(httpResponse);

            var (capacityKg, capacityM3) = await _truckServiceClient.GetTruckCapacityAsync(truckId, CancellationToken.None);

            Assert.Equal(0, capacityKg);
            Assert.Null(capacityM3);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("returned successful status code but content was null")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }


        [Fact]
        public async Task GetTruckCapacityAsync_HttpError_ReturnsZeroAndNull()
        {
            var truckId = Guid.NewGuid();
            var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            SetupHttpClient(httpResponse);

            var (capacityKg, capacityM3) = await _truckServiceClient.GetTruckCapacityAsync(truckId, CancellationToken.None);

            Assert.Equal(0, capacityKg);
            Assert.Null(capacityM3);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("GetTruckCapacityAsync failed for Truck")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        
        [Fact]
        public async Task GetTruckCapacityAsync_HttpRequestException_ReturnsZeroAndNull()
        {
            var truckId = Guid.NewGuid();
            var mockHttpMessageHandler = new MockHttpMessageHandler(async (request, token) =>
            {
                throw new HttpRequestException("Simulated request exception");
            });
        
            var httpClient = new HttpClient(mockHttpMessageHandler) { BaseAddress = new Uri("http://truckservice-api") };
            _mockHttpClientFactory.Setup(_ => _.CreateClient("TruckService")).Returns(httpClient);
            _truckServiceClient = new TruckServiceClient(_mockHttpClientFactory.Object, _mockLogger.Object);
            
            var (capacityKg, capacityM3) = await _truckServiceClient.GetTruckCapacityAsync(truckId, CancellationToken.None);

            Assert.Equal(0, capacityKg);
            Assert.Null(capacityM3);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.toString().Contains("HTTP request exception during GetTruckCapacityAsync")),
                    It.IsAny<HttpRequestException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
