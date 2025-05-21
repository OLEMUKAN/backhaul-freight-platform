using Microsoft.EntityFrameworkCore;
using TruckService.API.Models;

namespace TruckService.API.Data
{
    public class TruckDbContext : DbContext
    {
        public TruckDbContext(DbContextOptions<TruckDbContext> options) 
            : base(options)
        {
        }

        public DbSet<Truck> Trucks { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Truck entity
            modelBuilder.Entity<Truck>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.HasIndex(t => t.OwnerId);
                entity.HasIndex(t => t.RegistrationNumber).IsUnique();
                
                entity.Property(t => t.Make).IsRequired().HasMaxLength(100);
                entity.Property(t => t.Model).IsRequired().HasMaxLength(100);
                entity.Property(t => t.RegistrationNumber).IsRequired().HasMaxLength(50);
                
                entity.Property(t => t.CapacityKg).HasPrecision(10, 2);
                entity.Property(t => t.CapacityM3).HasPrecision(10, 2);
                entity.Property(t => t.CargoAreaLengthM).HasPrecision(6, 2);
                entity.Property(t => t.CargoAreaWidthM).HasPrecision(6, 2);
                entity.Property(t => t.CargoAreaHeightM).HasPrecision(6, 2);

                // Convert string array to JSON
                entity.Property(t => t.Photos).HasConversion(
                    v => string.Join(',', v ?? new string[0]),
                    v => string.IsNullOrEmpty(v) ? new string[0] : v.Split(',', StringSplitOptions.RemoveEmptyEntries));
            });
        }
    }
} 