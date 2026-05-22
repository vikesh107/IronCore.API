using IronCore.API.Data;
using IronCore.API.DTOs;
using IronCore.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IronCore.API.Services;

public interface IAuthService
{
    Task<(AuthResponse? response, string? error)> LoginAsync(LoginRequest req);
    Task<(AuthResponse? response, string? error)> RegisterOwnerAsync(RegisterOwnerRequest req);
    Task<(AuthResponse? response, string? error)> RegisterMemberAsync(RegisterMemberRequest req);
    Task<(AuthResponse? response, string? error)> AcceptInviteAsync(AcceptInviteRequest req);
    Task<(bool success, string? error)> ForgotPasswordAsync(string email);
    Task<(bool success, string? error)> ResetPasswordAsync(ResetPasswordRequest req);
    Task<(AuthResponse? response, string? error)> RefreshTokenAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string userId);
    Task<InviteInfoResponse?> GetInviteInfoAsync(string token);
    Task<(string Id, string Name)?> GetGymInfoAsync();
}

public class AuthService(
    UserManager<AppUser> userManager,
    AppDbContext db,
    ITokenService tokenService,
    IEmailService emailService) : IAuthService
{
    public async Task<(AuthResponse?, string?)> LoginAsync(LoginRequest req)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user == null || !user.IsActive)
            return (null, "Invalid email or password.");

        if (!await userManager.CheckPasswordAsync(user, req.Password))
            return (null, "Invalid email or password.");

        return (await BuildAuthResponse(user), null);
    }

    public async Task<(AuthResponse?, string?)> RegisterOwnerAsync(RegisterOwnerRequest req)
    {
        if (await userManager.FindByEmailAsync(req.Email) != null)
            return (null, "Email already registered.");

        var user = new AppUser
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = req.FullName,
            Role = UserRole.Owner,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return (null, string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, "Owner");

        var gym = new Gym
        {
            Name = req.GymName,
            Address = req.GymAddress,
            Phone = req.GymPhone,
            OwnerId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Gyms.Add(gym);
        await db.SaveChangesAsync();

        user.GymId = gym.Id;
        await userManager.UpdateAsync(user);

        await emailService.SendWelcomeOwnerAsync(user.Email, user.FullName, gym.Name);
        return (await BuildAuthResponse(user), null);
    }

    public async Task<(AuthResponse?, string?)> RegisterMemberAsync(RegisterMemberRequest req)
    {
        Gym? gym = string.IsNullOrWhiteSpace(req.GymId)
            ? await db.Gyms.OrderBy(g => g.CreatedAt).FirstOrDefaultAsync()
            : await db.Gyms.FindAsync(req.GymId);
        if (gym == null) return (null, "Gym not found.");

        if (await userManager.FindByEmailAsync(req.Email) != null)
            return (null, "Email already registered.");

        var user = new AppUser
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = req.FullName,
            Role = UserRole.Member,
            GymId = req.GymId,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return (null, string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, "Member");

        db.MemberProfiles.Add(new MemberProfile
        {
            UserId = user.Id,
            GymId = gym.Id,
            Phone = req.Phone
        });
        await db.SaveChangesAsync();

        return (await BuildAuthResponse(user), null);
    }

    public async Task<(AuthResponse?, string?)> AcceptInviteAsync(AcceptInviteRequest req)
    {
        var invite = await db.InviteTokens
            .FirstOrDefaultAsync(i => i.Token == req.Token && !i.IsUsed);

        if (invite == null) return (null, "Invalid or expired invite link.");
        if (invite.ExpiresAt < DateTime.UtcNow) return (null, "Invite link has expired.");
        if (await userManager.FindByEmailAsync(invite.Email) != null)
            return (null, "Email already registered.");

        var user = new AppUser
        {
            UserName = invite.Email,
            Email = invite.Email,
            FullName = req.FullName,
            Role = invite.Role,
            GymId = invite.GymId,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return (null, string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, invite.Role.ToString());

        // Create role profile
        if (invite.Role == UserRole.Trainer)
        {
            db.TrainerProfiles.Add(new TrainerProfile
            {
                UserId = user.Id,
                GymId = invite.GymId,
                Specialty = "General Fitness"
            });
        }
        else if (invite.Role == UserRole.Member)
        {
            db.MemberProfiles.Add(new MemberProfile
            {
                UserId = user.Id,
                GymId = invite.GymId,
                Phone = req.Phone
            });
        }

        invite.IsUsed = true;
        await db.SaveChangesAsync();

        var gym = await db.Gyms.FindAsync(invite.GymId);
        await emailService.SendWelcomeMemberAsync(user.Email, user.FullName, gym?.Name ?? "IronCore");

        return (await BuildAuthResponse(user), null);
    }

    public async Task<(bool, string?)> ForgotPasswordAsync(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null) return (true, null); // Don't reveal user existence

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await emailService.SendPasswordResetAsync(email, user.FullName, token);
        return (true, null);
    }

    public async Task<(bool, string?)> ResetPasswordAsync(ResetPasswordRequest req)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user == null) return (false, "Invalid request.");

        var result = await userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
        if (!result.Succeeded)
            return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

        return (true, null);
    }

    public async Task<(AuthResponse?, string?)> RefreshTokenAsync(string refreshToken)
    {
        var stored = await db.RefreshTokens
            .Include(r => r.User)  // pretend nav exists
            .FirstOrDefaultAsync(r => r.Token == refreshToken && !r.IsRevoked);

        if (stored == null || stored.ExpiresAt < DateTime.UtcNow)
            return (null, "Invalid or expired refresh token.");

        var user = await userManager.FindByIdAsync(stored.UserId);
        if (user == null || !user.IsActive) return (null, "User not found.");

        stored.IsRevoked = true;
        await db.SaveChangesAsync();

        return (await BuildAuthResponse(user), null);
    }

    public async Task RevokeRefreshTokenAsync(string userId)
    {
        var tokens = await db.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync();
        tokens.ForEach(t => t.IsRevoked = true);
        await db.SaveChangesAsync();
    }

    public async Task<(string Id, string Name)?> GetGymInfoAsync()
    {
        var gym = await db.Gyms.OrderBy(g => g.CreatedAt).FirstOrDefaultAsync();
        if (gym == null) return null;
        return (gym.Id, gym.Name);
    }

    public async Task<InviteInfoResponse?> GetInviteInfoAsync(string token)
    {
        var invite = await db.InviteTokens
            .Include(i => i.Gym)
            .FirstOrDefaultAsync(i => i.Token == token && !i.IsUsed && i.ExpiresAt > DateTime.UtcNow);

        if (invite == null) return null;
        return new InviteInfoResponse(invite.Email, invite.Role.ToString(), invite.Gym?.Name ?? "IronCore");
    }

    private async Task<AuthResponse> BuildAuthResponse(AppUser user)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = tokenService.GenerateRefreshToken();

        // Save refresh token
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email!,
            Role: user.Role.ToString(),
            GymId: user.GymId ?? string.Empty,
            ExpiresAt: DateTime.UtcNow.AddMinutes(60)
        );
    }
}
