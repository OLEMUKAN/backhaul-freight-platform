using ApiGateway.Middleware;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Ocelot.Cache.CacheManager;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Polly;
using Serilog;
using SharedSettings;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Disable default proxy settings to prevent proxy-related connection issues
System.Net.WebRequest.DefaultWebProxy = null;
HttpClient.DefaultProxy = new System.Net.WebProxy();

// Serilog configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting API Gateway");

// Ocelot configuration with additional modules
builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"ocelot.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Configure Ocelot with additional capabilities
builder.Services
    .AddOcelot(builder.Configuration)
    .AddCacheManager(x =>
    {
        x.WithDictionaryHandle();
    })
    .AddPolly();

// Rate limiting configuration
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("RateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// No Service Discovery Registration needed for this implementation

// JWT Authentication for Ocelot using shared settings 
// Note: We're adding JWT Bearer authentication with "Bearer" scheme to match Ocelot config
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var key = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT:Key not configured");
    var securityKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key));
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = securityKey,
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
    
    // Event handlers for better debugging of JWT issues
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context => 
        {
            Log.Warning("JWT Authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context => 
        {
            Log.Information("Token validated for user {UserId}", 
                context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return Task.CompletedTask;
        }
    };
});

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policyBuilder =>
    {
        policyBuilder
            .WithOrigins("http://localhost:3000", "https://localhost:3000", "http://localhost:4200", "https://localhost:4200") // Added common Angular port
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add controllers for gateway-specific endpoints
builder.Services.AddControllers();

// Add HTTP client factory for service communication
builder.Services.AddHttpClient();

// Configure HttpClientFactory to use proxy settings
builder.Services.ConfigureHttpClientDefaults(httpClientBuilder =>
{
    httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseProxy = false,
        Proxy = null
    });
});

// Swagger for API documentation and health check
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Backhaul Platform API Gateway", 
        Version = "v1",
        Description = "API Gateway for the Backhaul Freight Matching Platform"
    });
    
    // Add JWT authentication support to Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Health checks for the API Gateway and downstream services
builder.Services.AddHealthChecks();

var app = builder.Build();

// Custom API Gateway exception handler
app.UseMiddleware<ApiGateway.Middleware.ApiGatewayExceptionHandlerMiddleware>();

// Request logging
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "unknown");
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme ?? "unknown");
        diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    };
});

// Configure IpRateLimiting
app.UseIpRateLimiting();

// Development-specific configuration
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Backhaul API Gateway v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("CorsPolicy");

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers for gateway-specific endpoints
app.MapControllers();

// Basic health check endpoint
app.MapHealthChecks("/health");

// Enhanced aggregate health check that checks all downstream services
app.MapGet("/health/aggregate", async () =>
{
    using var httpClient = new HttpClient();
    var results = new List<object>();
    var servicesToCheck = new[]
    {
        new { Name = "UserService", Url = builder.Configuration["Services:UserService:BaseUrl"] + "/health" ?? "https://localhost:2999/health" },
        new { Name = "TruckService", Url = builder.Configuration["Services:TruckService:BaseUrl"] + "/health" ?? "https://localhost:7198/health" },
        new { Name = "RouteService", Url = builder.Configuration["Services:RouteService:BaseUrl"] + "/health" ?? "http://localhost:5003/health" }
    };
    
    foreach (var svc in servicesToCheck)
    {
        try
        {
            var resp = await httpClient.GetAsync(svc.Url);
            var content = await resp.Content.ReadAsStringAsync();
            results.Add(new { 
                svc.Name, 
                Status = resp.IsSuccessStatusCode ? "Healthy" : "Unhealthy", 
                StatusCode = (int)resp.StatusCode,
                Details = content 
            });
            
            Log.Information("Health check for {Service}: {Status}", svc.Name, resp.IsSuccessStatusCode ? "Healthy" : "Unhealthy");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Health check failed for {Service}", svc.Name);
            results.Add(new { 
                svc.Name, 
                Status = "Unreachable", 
                StatusCode = 503,
                Details = ex.Message 
            });
        }
    }
      var overall = results.All(r => ((string?)(r.GetType().GetProperty("Status")?.GetValue(r) ?? "Unhealthy")) == "Healthy") ? "Healthy" : "Degraded";
    return Results.Json(new { 
        overall, 
        timestamp = DateTime.UtcNow,
        services = results 
    });
})
.WithName("AggregateHealth");

try
{
    // Ocelot must be last in the middleware pipeline
    await app.UseOcelot();
    
    Log.Information("API Gateway started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
