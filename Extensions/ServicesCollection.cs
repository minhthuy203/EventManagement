using EventManagement.Models;
using Microsoft.AspNetCore.Identity;
using System.Configuration;
using System.Runtime.CompilerServices;

namespace EventManagement.Extensions
{
    public static class ServicesCollections
    {
        public static IServiceCollection MigrateDatabase(this IServiceCollection services)
        {
            using (var scope = services.BuildServiceProvider().CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<EventDbContext>();
                dbContext.Database.Migrate();
            }
            return services;
        }

        public static IServiceCollection AddAdminAndRole(this IServiceCollection services)
        {
            using (var scope = services.BuildServiceProvider().CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                // Tạo vai trò nếu chưa tồn tại
                if (!roleManager.RoleExistsAsync("Admin").Result)
                {
                    roleManager.CreateAsync(new IdentityRole("Admin")).Wait();
                }
                if (!roleManager.RoleExistsAsync("User").Result)
                {
                    roleManager.CreateAsync(new IdentityRole("User")).Wait();
                }
                if (!roleManager.RoleExistsAsync("Organizer").Result)
                {
                    roleManager.CreateAsync(new IdentityRole("Organizer")).Wait();
                }

                // Kiểm tra xem có tài khoản admin nào chưa
                if (userManager.FindByEmailAsync("mr.tuongqh1@gmail.com").Result == null)
                {
                    var adminUser = new ApplicationUser
                    {
                        FirstName = "Admin",
                        LastName = "Admin",
                        UserName = "mr.tuongqh1@gmail.com",
                        Email = "mr.tuongqh1@gmail.com",
                    };

                    var result = userManager.CreateAsync(adminUser, "Admin@123").Result;

                    if (result.Succeeded)
                    {
                        // Gán vai trò Admin cho tài khoản admin
                        userManager.AddToRoleAsync(adminUser, "Admin").Wait();
                    }
                }
            }
            return services;
        }
    }
}

