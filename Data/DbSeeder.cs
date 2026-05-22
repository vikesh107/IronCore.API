using IronCore.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IronCore.API.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var db = services.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        // Seed roles
        string[] roles = ["Owner", "Trainer", "Member"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed default Owner (change email/password before production!)
        var ownerEmail = "admin@ironcore.in";
        if (await userManager.FindByEmailAsync(ownerEmail) == null)
        {
            var owner = new AppUser
            {
                UserName = ownerEmail,
                Email = ownerEmail,
                FullName = "Gym Owner",
                Role = UserRole.Owner,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(owner, "Admin@12345");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(owner, "Owner");

                // Create gym
                var gym = new Gym
                {
                    Name = "IronCore Fitness",
                    OwnerId = owner.Id,
                    CreatedAt = DateTime.UtcNow
                };
                db.Gyms.Add(gym);
                await db.SaveChangesAsync();

                owner.GymId = gym.Id;
                await userManager.UpdateAsync(owner);
            }
        }
    }
}
