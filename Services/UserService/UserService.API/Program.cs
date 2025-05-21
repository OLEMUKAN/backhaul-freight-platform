using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using UserService.API.Data;
using UserService.API.Middleware;
using UserService.API.Models;
using UserService.API.Services;
using UserService.API.Events;
using UserService.API.Models.Enums;
using MassTransit;
using ServiceDiscovery;
using ServiceDiscovery.Middleware;
using System.Security.Claims;
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
    
    Log.Information("Starting User Service");
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
    builder.Services.AddInMemoryRateLimiting();

    // DB Context
    builder.Services.AddDbContext<UserDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Identity
    builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        // Password settings
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<UserDbContext>()
    .AddDefaultTokenProviders();

    // Authentication - using shared settings
    builder.Services.AddJwtAuthentication(builder.Configuration);

    // Configure MassTransit with RabbitMQ
    builder.Services.AddMassTransit(config =>
    {
        // Register consumers
        config.AddConsumer<TruckVerifiedConsumer>();
        config.AddConsumer<BookingCompletedConsumer>();

        // Configure RabbitMQ
        config.UsingRabbitMq((context, cfg) =>
        {
            var rabbitMqConfig = builder.Configuration.GetSection("RabbitMQ");
            var host = rabbitMqConfig["Host"] ?? "localhost";
            var username = rabbitMqConfig["Username"] ?? "guest";
            var password = rabbitMqConfig["Password"] ?? "guest";
            
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
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "User Service API", Version = "v1" });
        
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

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<UserDbContext>()
        .AddRabbitMQ(rabbitConnectionString: connectionString);    // Service discovery
    builder.Services.AddServiceDiscovery(builder.Configuration);
    
    // Register service-based HttpClients
    builder.Services.AddServiceHttpClient("TruckServiceClient", "TruckService");
    
    // Services
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserService, UserService.API.Services.UserService>();
    builder.Services.AddScoped<UserService.API.Services.IEventPublisher, UserService.API.Services.EventPublisher>();
    builder.Services.AddScoped<ServiceHttpClientFactory>();

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
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "User Service API v1"));
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
    
    // Seed ASP.NET Identity roles
    await RoleSeeder.SeedRolesAsync(app.Services);
    
    // Register service with service registry
    app.UseServiceRegistration("UserService");

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Endpoints
    app.MapControllers();
    app.MapHealthChecks("/health");

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
