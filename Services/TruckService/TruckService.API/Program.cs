using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TruckService.API.Data;
using Common.Messaging; // Added this line
using Common.Middleware;
using TruckService.API.Services;
using TruckService.API.Events; // Added for UserStatusChangedConsumer
using MassTransit;
using System.Security.Claims;
using AspNetCoreRateLimit;
using Serilog;
using Microsoft.Extensions.Azure;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using HealthChecks.RabbitMQ;
using RabbitMQ.Client;
using ServiceDiscovery;
using ServiceDiscovery.Middleware;
using System.IdentityModel.Tokens.Jwt;
using SharedSettings;

// Load configuration
var builder = WebApplication.CreateBuilder(args);

// Check if being executed by Entity Framework tools
bool isEfCommand = args.Any(a => a.Contains("ef"));

// Configure Serilog (but skip detailed logging for EF commands)
if (!isEfCommand)
{
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .CreateLogger();
    
    builder.Host.UseSerilog();
    
    Log.Information("Starting Truck Service");
}

try
{
    // Rate limiting
    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("RateLimiting"));
    builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
    builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
    builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
    
    // Database
    builder.Services.AddDbContext<TruckDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10, 
                maxRetryDelay: TimeSpan.FromSeconds(30), 
                errorNumbersToAdd: null)));
    
    // Azure Storage for truck photos and documents
    builder.Services.AddAzureClients(clientBuilder =>
    {
        clientBuilder.AddBlobServiceClient(builder.Configuration["Azure:Storage:ConnectionString"]);
    });
    
    // Authentication
    builder.Services.AddJwtAuthentication(builder.Configuration);
    
    // Configure MassTransit with RabbitMQ
    builder.Services.AddMassTransit(config =>
    {
        // Register consumers
        config.AddConsumer<UserStatusChangedConsumer>();

        // Configure RabbitMQ
        config.UsingRabbitMq((context, cfg) =>
        {
            var rabbitMqConfig = builder.Configuration.GetSection("RabbitMQ");
            var host = rabbitMqConfig["Host"] ?? "localhost";
            var username = rabbitMqConfig["Username"] ?? "guest";
            var password = rabbitMqConfig["Password"] ?? "guest";
            var port = rabbitMqConfig["Port"] ?? "5672";
            
            cfg.Host(host, "/", h =>
            {
                h.Username(username);
                h.Password(password);
            });
            
            // Configure endpoints for each consumer
            cfg.ConfigureEndpoints(context);
            
            // Error handling
            cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });
    });
    
    // API Controllers
    builder.Services.AddControllers();
    
    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", policyBuilder =>
        {
            policyBuilder
                .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "*" })
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });
    
    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Truck Service API", Version = "v1" });
        
        // Add JWT authentication to Swagger
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
                new List<string>()
            }
        });
    });
    
    // Health Checks
    var rabbitMqConfig = builder.Configuration.GetSection("RabbitMQ");
    var host = rabbitMqConfig["Host"] ?? "localhost";
    var username = rabbitMqConfig["Username"] ?? "guest";
    var password = rabbitMqConfig["Password"] ?? "guest";
    var port = rabbitMqConfig["Port"] ?? "5672";
    var connectionString = $"amqp://{username}:{password}@{host}:{port}/";
    
    // Register the IConnectionFactory as a singleton
    builder.Services.AddSingleton<IConnectionFactory>(sp =>
    {
        return new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            AutomaticRecoveryEnabled = true
        };
    });

      // Configure health checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
            name: "database",
            timeout: TimeSpan.FromSeconds(5),
            tags: new[] { "database", "sql" },
            failureStatus: HealthStatus.Degraded)
        .AddRabbitMQ(
            rabbitConnectionString: connectionString,
            name: "rabbitmq",
            timeout: TimeSpan.FromSeconds(3),
            tags: new[] { "messaging", "rabbitmq" });
    
    // Services
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ITruckService, TruckService.API.Services.TruckService>();
    builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
    builder.Services.AddScoped<IEventPublisher, Common.Messaging.EventPublisher>(); // Changed this line
    
    // Add service discovery
    builder.Services.AddServiceDiscovery(builder.Configuration);
    
    // Register service-based HttpClient
    builder.Services.AddServiceHttpClient("UserServiceClient", "UserService");
    
    // Add typed client service with service discovery
    builder.Services.AddScoped<IUserValidationService, UserValidationService>();
    
    // The ServiceHttpClientFactory is automatically registered by AddServiceDiscovery
    
    var app = builder.Build();
    
    // If running EF commands, exit early with success (migrations will handle creating/updating the database)
    if (isEfCommand)
    {
        return;
    }
    
    // Rate limiting
    app.UseIpRateLimiting();
    
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Truck Service API v1"));
    }
    else
    {
        // Use HSTS in production
        app.UseHsts();
    }
      // Exception handling middleware
    app.UseExceptionHandling();
    
    // HTTPS redirection
    app.UseHttpsRedirection();
    
    // Static files
    app.UseStaticFiles();
    
    // Use Serilog for request logging
    app.UseSerilogRequestLogging();
    
    // Routing
    app.UseRouting();
    
    // CORS
    app.UseCors("CorsPolicy");
    
    // Register service with service registry
    app.UseServiceRegistration("TruckService");
    
    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();
    
    // Endpoints
    app.MapControllers();
    
    // Configure detailed health check endpoint
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    exception = e.Value.Exception?.Message,
                    duration = e.Value.Duration.ToString()
                })
            };
            await context.Response.WriteAsJsonAsync(result);
        }
    });
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
