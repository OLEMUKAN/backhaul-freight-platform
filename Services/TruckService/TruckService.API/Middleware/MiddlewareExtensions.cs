using Microsoft.AspNetCore.Builder;

namespace TruckService.API.Middleware
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseTruckServiceExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
