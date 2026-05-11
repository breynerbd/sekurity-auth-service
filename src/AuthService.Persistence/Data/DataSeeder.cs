using AuthService.Domain.Entitis;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace AuthService.Persistence.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // 1️⃣ Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            var adminRole = new Role
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                Name = "ADMIN",
                Description = "Administrator role"
            };

            var userRole = new Role
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                Name = "USER",
                Description = "Standard user role"
            };

            await context.Roles.AddRangeAsync(adminRole, userRole);
            await context.SaveChangesAsync();
        }

        // 2️⃣ Seed Admin User
        if (!await context.Users.AnyAsync())
        {
            var adminRole = await context.Roles
                .FirstOrDefaultAsync(r => r.Name == "ADMIN");

            if (adminRole == null) return;

            var userId = Guid.NewGuid().ToString("N")[..16];

            var adminUser = new User
            {
                Id = userId,
                Name = "Admin",
                Surname = "User",
                Username = "admin",
                Email = "admin@sekuri.com",
                Password = BCrypt.Net.BCrypt.HashPassword("12345678"),
                Status = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                UserProfile = new UserProfile
                {
                    Id = Guid.NewGuid().ToString("N")[..16],
                    UserId = userId
                },

                UserEmail = new UserEmail
                {
                    Id = Guid.NewGuid().ToString("N")[..16],
                    UserId = userId,
                    EmailVerified = true
                },

                UserRoles = new List<UserRole>
                {
                    new UserRole
                    {
                        Id = Guid.NewGuid().ToString("N")[..16],
                        UserId = userId,
                        RoleId = adminRole.Id
                    }
                }
            };

            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();
        }
    }
}