using IronCore.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IronCore.API.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Gym> Gyms => Set<Gym>();
    public DbSet<TrainerProfile> TrainerProfiles => Set<TrainerProfile>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<InviteToken> InviteTokens => Set<InviteToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // AppUser → Gym (owner)
        builder.Entity<AppUser>()
            .HasOne(u => u.OwnedGym)
            .WithOne(g => g.Owner)
            .HasForeignKey<Gym>(g => g.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // AppUser → GymMembership
        builder.Entity<AppUser>()
            .HasOne(u => u.Gym)
            .WithMany(g => g.Members)
            .HasForeignKey(u => u.GymId)
            .OnDelete(DeleteBehavior.SetNull);

        // TrainerProfile → AppUser
        builder.Entity<TrainerProfile>()
            .HasOne(t => t.User)
            .WithOne(u => u.TrainerProfile)
            .HasForeignKey<TrainerProfile>(t => t.UserId);

        // MemberProfile → AppUser
        builder.Entity<MemberProfile>()
            .HasOne(m => m.User)
            .WithOne(u => u.MemberProfile)
            .HasForeignKey<MemberProfile>(m => m.UserId);

        // MemberProfile → TrainerProfile
        builder.Entity<MemberProfile>()
            .HasOne(m => m.Trainer)
            .WithMany(t => t.Clients)
            .HasForeignKey(m => m.TrainerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Subscription → MemberProfile
        builder.Entity<Subscription>()
            .HasOne(s => s.Member)
            .WithMany(m => m.Subscriptions)
            .HasForeignKey(s => s.MemberId);

        // Indexes
        builder.Entity<InviteToken>().HasIndex(i => i.Token).IsUnique();
        builder.Entity<RefreshToken>().HasIndex(r => r.Token).IsUnique();
        builder.Entity<Subscription>()
            .Property(s => s.PlanType)
            .HasConversion<string>();
        builder.Entity<Subscription>()
            .Property(s => s.Status)
            .HasConversion<string>();
        builder.Entity<AppUser>()
            .Property(u => u.Role)
            .HasConversion<string>();

        // InviteToken → Gym
        builder.Entity<InviteToken>()
            .HasOne(i => i.Gym)
            .WithMany()
            .HasForeignKey(i => i.GymId)
            .OnDelete(DeleteBehavior.Cascade);

        // RefreshToken → AppUser
        builder.Entity<RefreshToken>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
