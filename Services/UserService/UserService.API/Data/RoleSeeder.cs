using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using UserService.API.Models.Enums;

namespace UserService.API.Data
{
    public static class RoleSeeder
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<UserDbContext>>();

            string[] roleNames = Enum.GetNames(typeof(UserRole));
            
            foreach (var roleName in roleNames)
            {
                try
                {
                    var roleExists = await roleManager.RoleExistsAsync(roleName);
                    
                    if (!roleExists)
                    {
                        logger.LogInformation("Creating role: {Role}", roleName);
                        await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error seeding role {Role}", roleName);
                }
            }
        }
    }
} 