using System.Net;
using System.Text.Json;
using Serilog;

namespace ApiGateway.Middleware
{
    /// <summary>
    /// Custom exception handling middleware specifically designed for API Gateway scenarios
    /// </summary>
    public class ApiGatewayExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ApiGatewayExceptionHandlerMiddleware> _logger;

        public ApiGatewayExceptionHandlerMiddleware(
            RequestDelegate next,
            IWebHostEnvironment env,
            ILogger<ApiGatewayExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _env = env;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
                
                // Only handle non-success responses if the response hasn't started
                // and there's no content length header (indicating no response body)
                if (context.Response.StatusCode >= 400 && 
                    !context.Response.HasStarted && 
                    !context.Response.Headers.ContainsKey("Content-Length"))
                {
                    await HandleNonSuccessResponseAsync(context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in API Gateway");
                if (!context.Response.HasStarted)
                {
                    await HandleExceptionAsync(context, ex);
                }
                else
                {
                    _logger.LogWarning("Cannot handle exception: response has already started.");
                }
            }
        }

        private async Task HandleNonSuccessResponseAsync(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            
            var statusCode = context.Response.StatusCode;
            var errorMessage = statusCode switch
            {
                404 => "The requested resource was not found.",
                401 => "Authentication is required to access this resource.",
                403 => "You do not have permission to access this resource.",
                429 => "Too many requests. Please try again later.",
                _ => statusCode >= 500 
                    ? "An unexpected error occurred on the server." 
                    : "An error occurred processing your request."
            };

            var result = JsonSerializer.Serialize(new
            {
                StatusCode = statusCode,
                Message = errorMessage,
                TraceId = context.TraceIdentifier
            });

            // Clear any existing headers that might affect the response
            context.Response.Headers.Clear();
            context.Response.ContentType = "application/json";
            
            await context.Response.WriteAsync(result);
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                StatusCode = context.Response.StatusCode,
                Message = _env.IsDevelopment() 
                    ? $"Internal Server Error: {exception.Message}" 
                    : "An unexpected error occurred on the server.",
                TraceId = context.TraceIdentifier,
                Details = _env.IsDevelopment() ? exception.ToString() : null
            };

            // Clear any existing headers that might affect the response
            context.Response.Headers.Clear();
            context.Response.ContentType = "application/json";
            
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }

    // Extension method
    public static class ApiGatewayExceptionHandlerMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiGatewayExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiGatewayExceptionHandlerMiddleware>();
        }
    }
}
