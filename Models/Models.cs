using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace IronCore.API.Models;

// ── Enums ────────────────────────────────────────────────────────────────────
public enum UserRole { Owner, Trainer, Member }
public enum PlanType { Monthly, Quarterly, Yearly }
public enum SubscriptionStatus { Active, Expired, Pending, Cancelled }

// ── AppUser ──────────────────────────────────────────────────────────────────
public class AppUser : IdentityUser
{
    [MaxLength(100)] public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? GymId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string? ProfilePhotoUrl { get; set; }

    // Nav
    public Gym? OwnedGym { get; set; }
    public Gym? Gym { get; set; }
    public TrainerProfile? TrainerProfile { get; set; }
    public MemberProfile? MemberProfile { get; set; }
}

// ── Gym ──────────────────────────────────────────────────────────────────────
public class Gym
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string? Address { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Nav
    public AppUser Owner { get; set; } = null!;
    public ICollection<AppUser> Members { get; set; } = [];
    public ICollection<TrainerProfile> Trainers { get; set; } = [];
}

// ── TrainerProfile ────────────────────────────────────────────────────────────
public class TrainerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string GymId { get; set; } = string.Empty;
    [MaxLength(100)] public string Specialty { get; set; } = string.Empty;
    [MaxLength(500)] public string? Bio { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Nav
    public AppUser User { get; set; } = null!;
    public ICollection<MemberProfile> Clients { get; set; } = [];
}

// ── MemberProfile ─────────────────────────────────────────────────────────────
public class MemberProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string GymId { get; set; } = string.Empty;
    public string? TrainerId { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }
    [MaxLength(10)] public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Nav
    public AppUser User { get; set; } = null!;
    public TrainerProfile? Trainer { get; set; }
    public ICollection<Subscription> Subscriptions { get; set; } = [];

    // Computed (not mapped)
    public Subscription? ActiveSubscription =>
        Subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatus.Active);
}

// ── Subscription ──────────────────────────────────────────────────────────────
public class Subscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MemberId { get; set; } = string.Empty;
    public PlanType PlanType { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Nav
    public MemberProfile Member { get; set; } = null!;

    // Computed
    public int DaysLeft => Math.Max(0, (int)(EndDate - DateTime.UtcNow).TotalDays);
    public bool IsExpired => DateTime.UtcNow > EndDate;
}

// ── InviteToken ───────────────────────────────────────────────────────────────
public class InviteToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string GymId { get; set; } = string.Empty;
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public bool IsUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Gym? Gym { get; set; }
}

// ── RefreshToken ──────────────────────────────────────────────────────────────
public class RefreshToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
}
