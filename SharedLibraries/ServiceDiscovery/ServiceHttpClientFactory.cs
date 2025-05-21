using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;

namespace ServiceDiscovery
{    /// <summary>
    /// Factory for creating HttpClient instances that use the service registry
    /// </summary>
    public class ServiceHttpClientFactory
    {
        private readonly IServiceRegistry _serviceRegistry;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ServiceHttpClientFactory> _logger;
        
        // Retry policy for transient failures
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        
        // Circuit breaker policy to prevent cascading failures
        private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceHttpClientFactory"/> class
        /// </summary>
        /// <param name="serviceRegistry">The service registry</param>
        /// <param name="httpClientFactory">The HTTP client factory</param>
        /// <param name="logger">The logger</param>
        public ServiceHttpClientFactory(
            IServiceRegistry serviceRegistry,
            IHttpClientFactory httpClientFactory,
            ILogger<ServiceHttpClientFactory> logger)
        {
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Configure retry policy
            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timeSpan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(
                            "Request failed with {StatusCode}. Waiting {TimeSpan} before retry attempt {RetryAttempt}. Service: {Service}",
                            outcome.Result.StatusCode,
                            timeSpan,
                            retryAttempt,
                            context["ServiceName"]);
                    });
                    
            // Configure circuit breaker policy
            _circuitBreakerPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromMinutes(1),
                    onBreak: (outcome, breakDelay, context) =>
                    {
                        _logger.LogWarning(
                            "Circuit breaker opened for {BreakDelay}. Service: {Service}",
                            breakDelay,
                            context["ServiceName"]);
                            
                        // Update service health in registry if present
                        if (context.TryGetValue("ServiceName", out var serviceName))
                        {
                            try
                            {
                                _serviceRegistry.UpdateServiceHealth(serviceName.ToString(), ServiceHealthStatus.Unhealthy);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to update service health status for {Service}", serviceName);
                            }
                        }
                    },
                    onReset: context =>
                    {
                        _logger.LogInformation(
                            "Circuit breaker reset. Service: {Service}",
                            context["ServiceName"]);
                            
                        // Update service health in registry if present
                        if (context.TryGetValue("ServiceName", out var serviceName))
                        {
                            try
                            {
                                _serviceRegistry.UpdateServiceHealth(serviceName.ToString(), ServiceHealthStatus.Healthy);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to update service health status for {Service}", serviceName);
                            }
                        }
                    });
        }        /// <summary>
        /// Creates an HttpClient for a service
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <returns>An HttpClient configured for the service</returns>
        public HttpClient CreateClientForService(string serviceName)
        {
            try
            {
                // Get the client from the factory
                var client = _httpClientFactory.CreateClient(serviceName);
                
                // Set the base address if not already set
                if (client.BaseAddress == null)
                {
                    string baseUrl = _serviceRegistry.GetServiceBaseUrl(serviceName);
                    client.BaseAddress = new Uri(baseUrl);
                }
                
                // Check if the service is healthy before returning the client
                _ = CheckServiceHealthAsync(serviceName);
                
                return client;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating HTTP client for service {ServiceName}", serviceName);
                throw;
            }
        }
        
        /// <summary>
        /// Creates a resilient HTTP client for a service with retry and circuit breaker policies
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <returns>An HttpClient configured for the service with resiliency policies</returns>
        public HttpClient CreateResilientClientForService(string serviceName)
        {
            HttpClient client = CreateClientForService(serviceName);
            
            // Decorate the client with an HttpMessageHandler that applies our policies
            return new PolicyHttpClient(client, _retryPolicy, _circuitBreakerPolicy, serviceName, _logger);
        }
        
        /// <summary>
        /// Executes an HTTP request with resilience policies
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <param name="requestFunc">Function that executes the HTTP request</param>
        /// <returns>The HTTP response message</returns>
        public async Task<HttpResponseMessage> ExecuteWithResilienceAsync(
            string serviceName,
            Func<HttpClient, Task<HttpResponseMessage>> requestFunc)
        {
            // Get the standard client
            var client = CreateClientForService(serviceName);
            
            // Create context with service name for logging
            var context = new Context($"Service_{serviceName}")
            {
                ["ServiceName"] = serviceName
            };
            
            try
            {
                // Execute with both policies (circuit breaker wrapping retry)
                return await _circuitBreakerPolicy
                    .WrapAsync(_retryPolicy)
                    .ExecuteAsync(async (ctx) => await requestFunc(client), context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service call failed after applying resilience policies. Service: {Service}", serviceName);
                throw;
            }
        }
        
        /// <summary>
        /// Checks if a service is healthy and updates its status in the registry
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <returns>True if the service is healthy, false otherwise</returns>
        private async Task<bool> CheckServiceHealthAsync(string serviceName)
        {
            try
            {
                var isAvailable = await _serviceRegistry.IsServiceAvailableAsync(serviceName);
                return isAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check health for service {ServiceName}", serviceName);
                return false;
            }
        }
        
        /// <summary>
        /// HTTP client decorated with resilience policies
        /// </summary>
        private class PolicyHttpClient : HttpClient
        {
            private readonly HttpClient _innerClient;
            private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
            private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;
            private readonly string _serviceName;
            private readonly ILogger _logger;
            
            public PolicyHttpClient(
                HttpClient innerClient,
                AsyncRetryPolicy<HttpResponseMessage> retryPolicy,
                AsyncCircuitBreakerPolicy<HttpResponseMessage> circuitBreakerPolicy,
                string serviceName,
                ILogger logger)
            {
                _innerClient = innerClient;
                _retryPolicy = retryPolicy;
                _circuitBreakerPolicy = circuitBreakerPolicy;
                _serviceName = serviceName;
                _logger = logger;
                
                // Copy base properties from inner client
                BaseAddress = innerClient.BaseAddress;
                Timeout = innerClient.Timeout;
                MaxResponseContentBufferSize = innerClient.MaxResponseContentBufferSize;
            }
            
            public override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                System.Threading.CancellationToken cancellationToken)
            {
                // Create context with service name for logging
                var context = new Context($"Service_{_serviceName}")
                {
                    ["ServiceName"] = _serviceName
                };
                
                try
                {
                    // Execute with both policies (circuit breaker wrapping retry)
                    return await _circuitBreakerPolicy
                        .WrapAsync(_retryPolicy)
                        .ExecuteAsync(async (ctx) => 
                            await _innerClient.SendAsync(request, cancellationToken), 
                            context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Request failed after applying resilience policies. Service: {Service}, URL: {Url}", 
                        _serviceName, request.RequestUri);
                    throw;
                }
            }
        }
    }
}
