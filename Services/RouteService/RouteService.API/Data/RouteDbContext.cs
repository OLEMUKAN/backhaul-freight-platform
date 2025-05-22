using Microsoft.EntityFrameworkCore;
using RouteService.API.Models;

namespace RouteService.API.Data
{
    public class RouteDbContext : DbContext
    {
        public RouteDbContext(DbContextOptions<RouteDbContext> options) 
            : base(options)
        {
        }

        public DbSet<Models.Route> Routes { get; set; } = null!;        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Route entity
            modelBuilder.Entity<Models.Route>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.HasIndex(r => r.OwnerId);
                entity.HasIndex(r => r.TruckId);
                
                // Create an index on IsReturnLeg for filtering
                entity.HasIndex(r => r.IsReturnLeg);
                
                // Create a spatial index on OriginPoint
                entity.HasIndex(r => r.OriginPoint)
                      .HasMethod("GIST");
                
                // Create a spatial index on DestinationPoint
                entity.HasIndex(r => r.DestinationPoint)
                      .HasMethod("GIST");
                
                // Configure address fields
                entity.Property(r => r.OriginAddress).IsRequired().HasMaxLength(255);
                entity.Property(r => r.DestinationAddress).IsRequired().HasMaxLength(255);
                
                // Configure spatial properties
                entity.Property(r => r.OriginPoint).IsRequired().HasColumnType("geometry(Point, 4326)");
                entity.Property(r => r.DestinationPoint).IsRequired().HasColumnType("geometry(Point, 4326)");
                entity.Property(r => r.GeometryPath).HasColumnType("geometry(LineString, 4326)");
                
                // Configure capacity properties with precision
                entity.Property(r => r.CapacityAvailableKg).HasPrecision(10, 2);
                entity.Property(r => r.CapacityAvailableM3).HasPrecision(10, 2);
                entity.Property(r => r.TotalCapacityKg).HasPrecision(10, 2);
                entity.Property(r => r.TotalCapacityM3).HasPrecision(10, 2);
                entity.Property(r => r.EstimatedDistanceKm).HasPrecision(10, 2);
                
                // Configure timestamps
                entity.Property(r => r.CreatedAt).IsRequired();
                entity.Property(r => r.UpdatedAt).IsRequired();
            });
        }
    }
} 