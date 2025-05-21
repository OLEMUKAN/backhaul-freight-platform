using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;

namespace TruckService.API.Services
{
    public class CircuitBreakerPolicy
    {
        private readonly ILogger<CircuitBreakerPolicy> _logger;
        private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreaker;
        
        public CircuitBreakerPolicy(ILogger<CircuitBreakerPolicy> logger)
        {
            _logger = logger;
            
            // Configure the circuit breaker policy using HttpPolicyExtensions
            _circuitBreaker = HttpPolicyExtensions
                .HandleTransientHttpError() // This handles HttpRequestException, 5XX and 408 status codes
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (result, breakDelay) =>
                    {
                        _logger.LogWarning("Circuit breaker opened for {BreakDelay}s due to failures", breakDelay.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset, calls allowed again");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit breaker half-open, next call is a trial");
                    });
        }
        
        public AsyncCircuitBreakerPolicy<HttpResponseMessage> GetPolicy()
        {
            return _circuitBreaker;
        }
        
        public async Task<HttpResponseMessage> ExecuteAsync(Func<Task<HttpResponseMessage>> action)
        {
            return await _circuitBreaker.ExecuteAsync(action);
        }
    }
} 