using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RouteService.API.Data;
using Serilog;
using System.Text;
using MassTransit; // Required for AddMassTransit
using RouteService.API.Services; // For RouteService itself and MappingProfile
using RouteService.API.Services.Interfaces; // For IRouteService
using RouteService.API; // For MappingProfile
using RouteService.API.Consumers; // For MassTransit Consumers
using RouteService.API.Middleware; // For GlobalExceptionHandlerMiddleware
using SharedSettings; // Add this import for shared JWT settings

namespace RouteService.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container

builder.Services.AddMassTransit(mt =>
{
    // mt.SetKebabCaseEndpointNameFormatter(); // Optional: standard kebab-case for endpoint names

    // For now, no consumers are defined in this step.
    // Consumer registration will be handled in a later step.
    // Example: mt.AddConsumer<MyConsumer>();
    mt.AddConsumer<BookingConfirmedEventConsumer>();
    mt.AddConsumer<BookingCancelledEventConsumer>();

    mt.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], 
                 builder.Configuration["RabbitMQ:VirtualHost"], 
                 h =>
                 {
                     h.Username(builder.Configuration["RabbitMQ:Username"]);
                     h.Password(builder.Configuration["RabbitMQ:Password"]);
                 });

            // Add global incremental retry policy
            cfg.UseMessageRetry(r => 
            {
                r.Incremental(
                    retryLimit: 5,
                    initialInterval: TimeSpan.FromSeconds(1),
                    intervalIncrement: TimeSpan.FromSeconds(2)
                );
                // For now, no specific exception filters are added, making it a general retry.
                // Example for future: r.Handle<System.Net.Http.HttpRequestException>();
            });

        cfg.ConfigureEndpoints(context); // Ensure this is after global retry config
    });
});
// Optional: If you want MassTransit to start with the host and manage its lifecycle
// builder.Services.AddMassTransitHostedService(true); // This is often default with AddMassTransit in newer versions

builder.Services.AddControllers();

// Configure PostgreSQL with PostGIS
builder.Services.AddDbContext<RouteDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.UseNetTopologySuite()
    )
);

// Configure JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// Configure Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TruckOwner", policy => policy.RequireRole("TruckOwner"));
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Route Service API", Version = "v1" });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
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
            new string[] {}
        }
    });
});

// Configure HttpClient for Truck Service
builder.Services.AddHttpClient("TruckService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalServices:TruckService"]);
});

// Register application services
builder.Services.AddScoped<IGeospatialService, GeospatialService>();
builder.Services.AddScoped<ITruckServiceClient, TruckServiceClient>();
builder.Services.AddScoped<IEventPublisher, EventPublisher>();
builder.Services.AddScoped<IRouteService, RouteService.API.Services.RouteService>(); // Added IRouteService registration

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile)); // Added AutoMapper registration

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Register global exception handling middleware early in the pipeline
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

                app.MapControllers();

        app.Run();
    }
}
