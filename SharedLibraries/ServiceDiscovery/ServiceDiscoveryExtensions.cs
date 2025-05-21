using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Registry;
using Polly.Timeout;
using System;
using System.Net.Http;

namespace ServiceDiscovery
{
    // Context keys used for policy execution
    internal static class ContextKeys
    {
        public const string Logger = "Logger";
        public const string ServiceName = "ServiceName";
    }
    
    // Extension methods for Context to help with policy execution
    internal static class ContextExtensions
    {
        public static ILogger GetLogger(this Context context)
        {
            return context.ContainsKey(ContextKeys.Logger) 
                ? context[ContextKeys.Logger] as ILogger 
                : null;
        }
        
        public static string GetServiceName(this Context context)
        {
            return context.ContainsKey(ContextKeys.ServiceName) 
                ? context[ContextKeys.ServiceName] as string 
                : null;
        }
        
        public static Context WithLogger(this Context context, ILogger logger)
        {
            context[ContextKeys.Logger] = logger;
            return context;
        }
        
        public static Context WithServiceName(this Context context, string serviceName)
        {
            context[ContextKeys.ServiceName] = serviceName;
            return context;
        }
    }

    /// <summary>
    /// Extension methods for adding service discovery to the service collection
    /// </summary>
    public static class ServiceDiscoveryExtensions
    {        
        /// <summary>
        /// Adds service discovery services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection</returns>
        public static IServiceCollection AddServiceDiscovery(this IServiceCollection services, IConfiguration configuration)
        {
            // Create a policy registry
            var registry = services.AddPolicyRegistry();
            
            // Add standard policies to registry
            registry.Add("RetryPolicy", GetRetryPolicy());
            registry.Add("CircuitBreakerPolicy", GetCircuitBreakerPolicy());
            registry.Add("TimeoutPolicy", GetTimeoutPolicy());
            
            // Register HttpClient for health checks with a shorter timeout
            services.AddHttpClient<ConfigurationServiceRegistry>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            }).AddPolicyHandler(GetRetryPolicy());

            // Register the service registry
            services.AddSingleton<IServiceRegistry, ConfigurationServiceRegistry>();
            
            // Register additional service factories
            services.AddScoped<ServiceHttpClientFactory>();

            return services;
        }        
        /// <summary>
        /// Adds a named HttpClient that resolves the base address from the service registry
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="httpClientName">The name of the HttpClient</param>
        /// <param name="serviceName">The name of the service in the registry</param>
        /// <returns>The service collection</returns>
        public static IServiceCollection AddServiceHttpClient(
            this IServiceCollection services,
            string httpClientName,
            string serviceName)
        {
            services.AddHttpClient(httpClientName)
                .AddPolicyHandler((provider, _) => 
                {
                    var logger = provider.GetService<ILogger<ServiceHttpClientFactory>>();
                    return Policy.WrapAsync(
                        GetRetryPolicy(), 
                        GetCircuitBreakerPolicy(serviceName, provider.GetService<IServiceRegistry>(), logger),
                        GetTimeoutPolicy());
                })
                .ConfigureHttpClient((serviceProvider, client) =>
                {
                    var serviceRegistry = serviceProvider.GetRequiredService<IServiceRegistry>();
                    var baseUrl = serviceRegistry.GetServiceBaseUrl(serviceName);
                    client.BaseAddress = new Uri(baseUrl);
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                });

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .WaitAndRetryAsync(
                    3, 
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        var logger = context.GetLogger();
                        var serviceName = context.GetServiceName();
                        
                        if (logger != null)
                        {
                            logger.LogWarning(
                                "Request failed with {StatusCode}. Waiting {TimeSpan} before retry attempt {RetryAttempt}. Service: {Service}",
                                outcome.Result?.StatusCode,
                                timespan,
                                retryAttempt,
                                serviceName ?? "unknown");
                        }
                    });
        }
        
        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
            string serviceName = null,
            IServiceRegistry serviceRegistry = null,
            ILogger logger = null)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromMinutes(1),
                    onBreak: (outcome, breakDelay, context) =>
                    {
                        var loggerFromContext = context.GetLogger() ?? logger;
                        var serviceNameFromContext = context.GetServiceName() ?? serviceName;
                        
                        if (loggerFromContext != null)
                        {
                            loggerFromContext.LogWarning(
                                "Circuit breaker opened for {BreakDelay}. Service: {Service}",
                                breakDelay,
                                serviceNameFromContext ?? "unknown");
                        }
                        
                        // Update service health in registry if available
                        if (serviceRegistry != null && !string.IsNullOrEmpty(serviceNameFromContext))
                        {
                            try
                            {
                                serviceRegistry.UpdateServiceHealth(serviceNameFromContext, ServiceHealthStatus.Unhealthy);
                            }
                            catch (Exception ex)
                            {
                                if (loggerFromContext != null)
                                {
                                    loggerFromContext.LogError(ex, 
                                        "Failed to update service health status for {Service}", 
                                        serviceNameFromContext);
                                }
                            }
                        }
                    },
                    onReset: context =>
                    {
                        var loggerFromContext = context.GetLogger() ?? logger;
                        var serviceNameFromContext = context.GetServiceName() ?? serviceName;
                        
                        if (loggerFromContext != null)
                        {
                            loggerFromContext.LogInformation(
                                "Circuit breaker reset. Service: {Service}",
                                serviceNameFromContext ?? "unknown");
                        }
                        
                        // Update service health in registry if available
                        if (serviceRegistry != null && !string.IsNullOrEmpty(serviceNameFromContext))
                        {
                            try
                            {
                                serviceRegistry.UpdateServiceHealth(serviceNameFromContext, ServiceHealthStatus.Healthy);
                            }
                            catch (Exception ex)
                            {
                                if (loggerFromContext != null)
                                {
                                    loggerFromContext.LogError(ex, 
                                        "Failed to update service health status for {Service}", 
                                        serviceNameFromContext);
                                }
                            }
                        }
                    });
        }
        
        private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10));
        }
    }
}
