using IronCore.API.Data;
using IronCore.API.DTOs;
using IronCore.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IronCore.API.Services;

// ── Trainer Service ───────────────────────────────────────────────────────────
public interface ITrainerService
{
    Task<List<TrainerDto>> GetAllAsync(string gymId);
    Task<TrainerDto?> GetByIdAsync(string trainerId);
    Task<(TrainerDto?, string?)> CreateDirectlyAsync(string gymId, CreateTrainerRequest req);
    Task<(bool, string?)> InviteAsync(string email, string gymId, IEmailService emailService, IConfiguration config);
    Task<(bool, string?)> UpdateAsync(string trainerId, UpdateTrainerRequest req);
    Task<(bool, string?)> UpdatePasswordAsync(string trainerId, string newPassword);
    Task<(bool, string?)> DeleteAsync(string trainerId);
}

public class TrainerService(AppDbContext db, UserManager<AppUser> userManager) : ITrainerService
{
    public async Task<List<TrainerDto>> GetAllAsync(string gymId) =>
        await db.TrainerProfiles
            .Where(t => t.GymId == gymId)
            .Include(t => t.User)
            .Include(t => t.Clients)
            .Select(t => MapToDto(t))
            .ToListAsync();

    public async Task<TrainerDto?> GetByIdAsync(string trainerId)
    {
        var t = await db.TrainerProfiles
            .Include(t => t.User)
            .Include(t => t.Clients)
            .FirstOrDefaultAsync(t => t.Id == trainerId);
        return t == null ? null : MapToDto(t);
    }

    public async Task<(TrainerDto?, string?)> CreateDirectlyAsync(string gymId, CreateTrainerRequest req)
    {
        if (await userManager.FindByEmailAsync(req.Email) != null)
            return (null, "Email already registered.");

        var gym = await db.Gyms.FindAsync(gymId);
        if (gym == null) return (null, "Gym not found.");

        var user = new AppUser
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = req.FullName,
            Role = UserRole.Trainer,
            GymId = gymId,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return (null, string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, "Trainer");

        var profile = new TrainerProfile
        {
            UserId = user.Id,
            GymId = gymId,
            Specialty = req.Specialty ?? "General Fitness",
            Bio = req.Bio
        };
        db.TrainerProfiles.Add(profile);
        await db.SaveChangesAsync();

        var created = await db.TrainerProfiles
            .Include(t => t.User)
            .Include(t => t.Clients)
            .FirstAsync(t => t.Id == profile.Id);

        return (MapToDto(created), null);
    }

    public async Task<(bool, string?)> InviteAsync(string email, string gymId, IEmailService emailService, IConfiguration config)
    {
        var gym = await db.Gyms.FindAsync(gymId);
        if (gym == null) return (false, "Gym not found.");

        var invite = new InviteToken
        {
            Email = email,
            Role = UserRole.Trainer,
            GymId = gymId
        };
        db.InviteTokens.Add(invite);
        await db.SaveChangesAsync();

        var frontendUrl = config["Frontend:Url"] ?? "http://localhost:4200";
        var link = $"{frontendUrl}/accept-invite?token={invite.Token}";
        await emailService.SendInviteAsync(email, "Trainer", link, gym.Name);
        return (true, null);
    }

    public async Task<(bool, string?)> UpdateAsync(string trainerId, UpdateTrainerRequest req)
    {
        var trainer = await db.TrainerProfiles.FindAsync(trainerId);
        if (trainer == null) return (false, "Trainer not found.");
        if (req.Specialty != null) trainer.Specialty = req.Specialty;
        if (req.Bio != null) trainer.Bio = req.Bio;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool, string?)> UpdatePasswordAsync(string trainerId, string newPassword)
    {
        var trainer = await db.TrainerProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == trainerId);
        if (trainer == null) return (false, "Trainer not found.");
        var token = await userManager.GeneratePasswordResetTokenAsync(trainer.User);
        var result = await userManager.ResetPasswordAsync(trainer.User, token, newPassword);
        return result.Succeeded ? (true, null) : (false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    public async Task<(bool, string?)> DeleteAsync(string trainerId)
    {
        var trainer = await db.TrainerProfiles
            .Include(t => t.Clients)
            .FirstOrDefaultAsync(t => t.Id == trainerId);
        if (trainer == null) return (false, "Trainer not found.");

        // Unassign clients
        foreach (var client in trainer.Clients)
            client.TrainerId = null;

        var user = await db.Users.FindAsync(trainer.UserId);
        if (user != null) user.IsActive = false;

        db.TrainerProfiles.Remove(trainer);
        await db.SaveChangesAsync();
        return (true, null);
    }

    private static TrainerDto MapToDto(TrainerProfile t) => new(
        t.Id, t.UserId, t.User.FullName, t.User.Email!,
        t.Specialty, t.Bio, t.Clients.Count, t.JoinedAt, t.User.IsActive
    );
}

// ── Member Service ────────────────────────────────────────────────────────────
public interface IMemberService
{
    Task<List<MemberDto>> GetAllAsync(string gymId);
    Task<List<MemberDto>> GetByTrainerAsync(string trainerId);
    Task<MemberDto?> GetByUserIdAsync(string userId);
    Task<(MemberDto?, string?)> CreateDirectlyAsync(string gymId, CreateMemberRequest req);
    Task<(bool, string?)> InviteAsync(string email, string gymId, IEmailService emailService, IConfiguration config);
    Task<(bool, string?)> AssignTrainerAsync(AssignTrainerRequest req);
    Task<(bool, string?)> UpdateAsync(string memberId, UpdateMemberRequest req);
    Task<(bool, string?)> UpdatePasswordAsync(string memberId, string newPassword);
    Task<(bool, string?)> DeleteAsync(string memberId);
}

public class MemberService(AppDbContext db, UserManager<AppUser> userManager) : IMemberService
{
    public async Task<List<MemberDto>> GetAllAsync(string gymId) =>
        await db.MemberProfiles
            .Where(m => m.GymId == gymId)
            .Include(m => m.User)
            .Include(m => m.Trainer).ThenInclude(t => t!.User)
            .Include(m => m.Subscriptions)
            .Select(m => MapToDto(m))
            .ToListAsync();

    public async Task<List<MemberDto>> GetByTrainerAsync(string userId)
    {
        var profile = await db.TrainerProfiles.FirstOrDefaultAsync(t => t.UserId == userId);
        if (profile == null) return [];
        return await db.MemberProfiles
            .Where(m => m.TrainerId == profile.Id)
            .Include(m => m.User)
            .Include(m => m.Trainer).ThenInclude(t => t!.User)
            .Include(m => m.Subscriptions)
            .Select(m => MapToDto(m))
            .ToListAsync();
    }

    public async Task<MemberDto?> GetByUserIdAsync(string userId)
    {
        var m = await db.MemberProfiles
            .Include(m => m.User)
            .Include(m => m.Trainer).ThenInclude(t => t!.User)
            .Include(m => m.Subscriptions)
            .FirstOrDefaultAsync(m => m.UserId == userId);
        return m == null ? null : MapToDto(m);
    }

    public async Task<(MemberDto?, string?)> CreateDirectlyAsync(string gymId, CreateMemberRequest req)
    {
        if (await userManager.FindByEmailAsync(req.Email) != null)
            return (null, "Email already registered.");

        var gym = await db.Gyms.FindAsync(gymId);
        if (gym == null) return (null, "Gym not found.");

        var user = new AppUser
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = req.FullName,
            Role = UserRole.Member,
            GymId = gymId,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return (null, string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, "Member");

        var profile = new MemberProfile
        {
            UserId = user.Id,
            GymId = gymId,
            Phone = req.Phone
        };
        db.MemberProfiles.Add(profile);
        await db.SaveChangesAsync();

        var created = await db.MemberProfiles
            .Include(m => m.User)
            .Include(m => m.Trainer).ThenInclude(t => t!.User)
            .Include(m => m.Subscriptions)
            .FirstAsync(m => m.Id == profile.Id);

        return (MapToDto(created), null);
    }

    public async Task<(bool, string?)> InviteAsync(string email, string gymId, IEmailService emailService, IConfiguration config)
    {
        var gym = await db.Gyms.FindAsync(gymId);
        if (gym == null) return (false, "Gym not found.");

        var invite = new InviteToken { Email = email, Role = UserRole.Member, GymId = gymId };
        db.InviteTokens.Add(invite);
        await db.SaveChangesAsync();

        var link = $"{config["Frontend:Url"] ?? "http://localhost:4200"}/accept-invite?token={invite.Token}";
        await emailService.SendInviteAsync(email, "Member", link, gym.Name);
        return (true, null);
    }

    public async Task<(bool, string?)> AssignTrainerAsync(AssignTrainerRequest req)
    {
        var member = await db.MemberProfiles.FindAsync(req.MemberId);
        if (member == null) return (false, "Member not found.");
        member.TrainerId = req.TrainerId;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool, string?)> UpdateAsync(string memberId, UpdateMemberRequest req)
    {
        var member = await db.MemberProfiles.FindAsync(memberId);
        if (member == null) return (false, "Member not found.");
        if (req.Phone != null) member.Phone = req.Phone;
        if (req.Gender != null) member.Gender = req.Gender;
        if (req.DateOfBirth != null) member.DateOfBirth = req.DateOfBirth;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool, string?)> UpdatePasswordAsync(string memberId, string newPassword)
    {
        var member = await db.MemberProfiles.Include(m => m.User).FirstOrDefaultAsync(m => m.Id == memberId);
        if (member == null) return (false, "Member not found.");
        var token = await userManager.GeneratePasswordResetTokenAsync(member.User);
        var result = await userManager.ResetPasswordAsync(member.User, token, newPassword);
        return result.Succeeded ? (true, null) : (false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    public async Task<(bool, string?)> DeleteAsync(string memberId)
    {
        var member = await db.MemberProfiles
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == memberId);
        if (member == null) return (false, "Member not found.");
        member.User.IsActive = false;
        await db.SaveChangesAsync();
        return (true, null);
    }

    private static MemberDto MapToDto(MemberProfile m)
    {
        var activeSub = m.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefault();
        return new MemberDto(
            m.Id, m.UserId, m.User.FullName, m.User.Email!,
            m.Phone, m.TrainerId, m.Trainer?.User.FullName,
            activeSub == null ? null : MapSubToDto(activeSub),
            m.JoinedAt, m.User.IsActive
        );
    }

    private static SubscriptionDto MapSubToDto(Subscription s) => new(
        s.Id, s.PlanType.ToString(), s.Status.ToString(),
        s.StartDate, s.EndDate, s.Amount, s.DaysLeft, s.Notes
    );
}

// ── Subscription Service ──────────────────────────────────────────────────────
public interface ISubscriptionService
{
    Task<List<SubscriptionDto>> GetByMemberAsync(string memberId);
    Task<(SubscriptionDto?, string?)> CreateAsync(CreateSubscriptionRequest req);
    Task<(bool, string?)> UpdateStatusAsync(string subscriptionId, string status);
}

public class SubscriptionService(AppDbContext db) : ISubscriptionService
{
    private static readonly Dictionary<PlanType, (int months, decimal price)> PlanConfig = new()
    {
        [PlanType.Monthly]   = (1,  999m),
        [PlanType.Quarterly] = (3,  2699m),
        [PlanType.Yearly]    = (12, 9999m),
    };

    public async Task<List<SubscriptionDto>> GetByMemberAsync(string memberId) =>
        await db.Subscriptions
            .Where(s => s.MemberId == memberId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SubscriptionDto(
                s.Id, s.PlanType.ToString(), s.Status.ToString(),
                s.StartDate, s.EndDate, s.Amount, s.DaysLeft, s.Notes))
            .ToListAsync();

    public async Task<(SubscriptionDto?, string?)> CreateAsync(CreateSubscriptionRequest req)
    {
        if (!Enum.TryParse<PlanType>(req.PlanType, out var plan))
            return (null, "Invalid plan type. Use Monthly, Quarterly, or Yearly.");

        var member = await db.MemberProfiles.FindAsync(req.MemberId);
        if (member == null) return (null, "Member not found.");

        // Expire existing active subscription
        var existing = await db.Subscriptions
            .Where(s => s.MemberId == req.MemberId && s.Status == SubscriptionStatus.Active)
            .ToListAsync();
        existing.ForEach(s => s.Status = SubscriptionStatus.Cancelled);

        var (months, defaultPrice) = PlanConfig[plan];
        var sub = new Subscription
        {
            MemberId = req.MemberId,
            PlanType = plan,
            Status = SubscriptionStatus.Active,
            StartDate = req.StartDate,
            EndDate = req.StartDate.AddMonths(months),
            Amount = req.Amount ?? defaultPrice,
            Notes = req.Notes
        };

        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        return (new SubscriptionDto(sub.Id, sub.PlanType.ToString(), sub.Status.ToString(),
            sub.StartDate, sub.EndDate, sub.Amount, sub.DaysLeft, sub.Notes), null);
    }

    public async Task<(bool, string?)> UpdateStatusAsync(string subscriptionId, string status)
    {
        if (!Enum.TryParse<SubscriptionStatus>(status, out var s))
            return (false, "Invalid status.");
        var sub = await db.Subscriptions.FindAsync(subscriptionId);
        if (sub == null) return (false, "Subscription not found.");
        sub.Status = s;
        await db.SaveChangesAsync();
        return (true, null);
    }
}

// ── Dashboard Service ─────────────────────────────────────────────────────────
public interface IDashboardService
{
    Task<OwnerDashboardDto> GetOwnerDashboardAsync(string gymId);
    Task<TrainerDashboardDto> GetTrainerDashboardAsync(string trainerId);
    Task<MemberDashboardDto> GetMemberDashboardAsync(string userId);
}

public class DashboardService(AppDbContext db, IMemberService memberService) : IDashboardService
{
    public async Task<OwnerDashboardDto> GetOwnerDashboardAsync(string gymId)
    {
        var members = await db.MemberProfiles
            .Where(m => m.GymId == gymId)
            .Include(m => m.User)
            .Include(m => m.Subscriptions)
            .ToListAsync();

        var trainers = await db.TrainerProfiles
            .Where(t => t.GymId == gymId)
            .Include(t => t.User)
            .Include(t => t.Clients)
            .ToListAsync();

        var allSubs = members.SelectMany(m => m.Subscriptions).ToList();
        var activeSubs = allSubs.Where(s => s.Status == SubscriptionStatus.Active).ToList();
        var now = DateTime.UtcNow;
        var weekLater = now.AddDays(7);

        var revenue = activeSubs.Sum(s => s.Amount);

        var trainerLoads = trainers.Select(t => new TrainerLoadDto(
            t.Id, t.User.FullName, t.Clients.Count,
            t.Clients.Count(c => c.Subscriptions.Any(s => s.Status == SubscriptionStatus.Active))
        )).ToList();

        var recentActivities = members
            .OrderByDescending(m => m.JoinedAt)
            .Take(5)
            .Select(m => new RecentActivityDto("join", $"{m.User.FullName} joined the gym", m.JoinedAt))
            .ToList();

        return new OwnerDashboardDto(
            TotalMembers: members.Count,
            ActiveMembers: members.Count(m => m.Subscriptions.Any(s => s.Status == SubscriptionStatus.Active)),
            ExpiredMembers: members.Count(m => !m.Subscriptions.Any(s => s.Status == SubscriptionStatus.Active)),
            TotalTrainers: trainers.Count,
            UnassignedMembers: members.Count(m => m.TrainerId == null),
            ExpiringThisWeek: activeSubs.Count(s => s.EndDate >= now && s.EndDate <= weekLater),
            EstimatedMonthlyRevenue: revenue,
            TrainerLoads: trainerLoads,
            RecentActivities: recentActivities
        );
    }

    public async Task<TrainerDashboardDto> GetTrainerDashboardAsync(string userId)
    {
        var trainerProfile = await db.TrainerProfiles.FirstOrDefaultAsync(t => t.UserId == userId);
        var profileId = trainerProfile?.Id ?? string.Empty;

        var clients = await db.MemberProfiles
            .Where(m => m.TrainerId == profileId)
            .Include(m => m.User)
            .Include(m => m.Subscriptions)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var memberDtos = clients.Select(m => new MemberDto(
            m.Id, m.UserId, m.User.FullName, m.User.Email!, m.Phone,
            m.TrainerId, null,
            m.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active)
                .OrderByDescending(s => s.EndDate).FirstOrDefault() is Subscription s
                ? new SubscriptionDto(s.Id, s.PlanType.ToString(), s.Status.ToString(),
                    s.StartDate, s.EndDate, s.Amount, s.DaysLeft, s.Notes) : null,
            m.JoinedAt, m.User.IsActive
        )).ToList();

        return new TrainerDashboardDto(
            clients.Count,
            clients.Count(m => m.Subscriptions.Any(s => s.Status == SubscriptionStatus.Active)),
            clients.Count(m => m.Subscriptions.Any(s =>
                s.Status == SubscriptionStatus.Active &&
                s.EndDate >= now && s.EndDate <= now.AddDays(7))),
            memberDtos
        );
    }

    public async Task<MemberDashboardDto> GetMemberDashboardAsync(string userId)
    {
        var profile = await memberService.GetByUserIdAsync(userId);
        if (profile == null) throw new Exception("Member profile not found.");

        var history = await db.Subscriptions
            .Where(s => s.MemberId == profile.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SubscriptionDto(s.Id, s.PlanType.ToString(), s.Status.ToString(),
                s.StartDate, s.EndDate, s.Amount, s.DaysLeft, s.Notes))
            .ToListAsync();

        return new MemberDashboardDto(profile, profile.ActiveSubscription, history);
    }
}
