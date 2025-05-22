using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace RouteService.API.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception has occurred: {Message}", ex.Message);

                context.Response.ContentType = "application/json";
                var statusCode = StatusCodes.Status500InternalServerError; // Default
                var message = "An internal server error occurred.";

                switch (ex)
                {
                    case ArgumentException argumentException:
                    case ValidationException validationException: // System.ComponentModel.DataAnnotations.ValidationException
                        statusCode = StatusCodes.Status400BadRequest;
                        message = ex.Message; // Use the specific exception message
                        break;
                    case UnauthorizedAccessException unauthorizedAccessException:
                        statusCode = StatusCodes.Status401Unauthorized;
                        message = "Unauthorized access.";
                        break;
                    case KeyNotFoundException keyNotFoundException:
                        statusCode = StatusCodes.Status404NotFound;
                        message = ex.Message; // Or a generic "Resource not found"
                        break;
                    case OperationCanceledException operationCanceledException:
                        statusCode = 499; // Client Closed Request (non-standard, but common)
                        message = "Request was cancelled by the client.";
                        break;
                    // Add more specific exception cases if needed
                }

                context.Response.StatusCode = statusCode;

                var errorResponse = new
                {
                    statusCode = context.Response.StatusCode,
                    message = message
                };

                var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase // Standard JSON naming
                });

                await context.Response.WriteAsync(jsonResponse);
            }
        }
    }
}
