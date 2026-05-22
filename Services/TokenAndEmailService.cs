using IronCore.API.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace IronCore.API.Services;

// ── Token Service ─────────────────────────────────────────────────────────────
public interface ITokenService
{
    string GenerateAccessToken(AppUser user);
    string GenerateRefreshToken();
}

public class TokenService(IConfiguration config) : ITokenService
{
    public string GenerateAccessToken(AppUser user)
    {
        var jwtSettings = config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("gymId", user.GymId ?? string.Empty),
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}

// ── Email Service (via Resend.com REST API) ───────────────────────────────────
public interface IEmailService
{
    Task SendInviteAsync(string toEmail, string role, string inviteLink, string gymName);
    Task SendWelcomeOwnerAsync(string email, string name, string gymName);
    Task SendWelcomeMemberAsync(string email, string name, string gymName);
    Task SendPasswordResetAsync(string email, string name, string resetLink);
    Task SendSubscriptionExpiryReminderAsync(string email, string name, int daysLeft);
}

public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    private readonly string _apiKey = config["Resend:ApiKey"] ?? string.Empty;
    private readonly string _fromEmail = config["Resend:FromEmail"] ?? "noreply@ironcore.in";
    private readonly string _frontendUrl = config["Frontend:Url"] ?? "http://localhost:4200";

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            logger.LogWarning("Resend API key not configured. Email to {To} skipped.", to);
            return;
        }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        var payload = new
        {
            from = _fromEmail,
            to = new[] { to },
            subject,
            html = htmlBody
        };

        var response = await http.PostAsJsonAsync("https://api.resend.com/emails", payload);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            logger.LogError("Resend email failed: {Error}", error);
        }
    }

    public Task SendInviteAsync(string toEmail, string role, string inviteLink, string gymName) =>
        SendAsync(toEmail, $"You're invited to join {gymName}!", $"""
            <div style="font-family:sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#111">You're invited! 💪</h2>
              <p>You've been invited to join <strong>{gymName}</strong> as a <strong>{role}</strong>.</p>
              <p>Click the button below to set up your account:</p>
              <a href="{inviteLink}" style="display:inline-block;background:#4ade80;color:#111;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:600;margin:16px 0">
                Accept Invitation
              </a>
              <p style="color:#888;font-size:12px">This link expires in 7 days.</p>
            </div>
        """);

    public Task SendWelcomeOwnerAsync(string email, string name, string gymName) =>
        SendAsync(email, $"Welcome to IronCore — {gymName} is live!", $"""
            <div style="font-family:sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2>Welcome, {name}! 🏋️</h2>
              <p>Your gym <strong>{gymName}</strong> has been set up on IronCore.</p>
              <p>You can now add trainers and members from your dashboard.</p>
              <a href="{_frontendUrl}/dashboard" style="display:inline-block;background:#4ade80;color:#111;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:600">
                Go to Dashboard
              </a>
            </div>
        """);

    public Task SendWelcomeMemberAsync(string email, string name, string gymName) =>
        SendAsync(email, $"Welcome to {gymName}!", $"""
            <div style="font-family:sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2>Welcome, {name}! 💪</h2>
              <p>Your account at <strong>{gymName}</strong> is ready.</p>
              <a href="{_frontendUrl}/login" style="display:inline-block;background:#4ade80;color:#111;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:600">
                Login Now
              </a>
            </div>
        """);

    public Task SendPasswordResetAsync(string email, string name, string resetToken) =>
        SendAsync(email, "Reset your IronCore password", $"""
            <div style="font-family:sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2>Password Reset 🔑</h2>
              <p>Hi {name}, here's your password reset link:</p>
              <a href="{_frontendUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(email)}"
                 style="display:inline-block;background:#4ade80;color:#111;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:600">
                Reset Password
              </a>
              <p style="color:#888;font-size:12px">Expires in 1 hour. If you didn't request this, ignore this email.</p>
            </div>
        """);

    public Task SendSubscriptionExpiryReminderAsync(string email, string name, int daysLeft) =>
        SendAsync(email, $"Your gym subscription expires in {daysLeft} days!", $"""
            <div style="font-family:sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2>Subscription Reminder ⏰</h2>
              <p>Hi {name}, your gym subscription expires in <strong>{daysLeft} days</strong>.</p>
              <p>Contact your gym owner to renew and keep your fitness journey going!</p>
            </div>
        """);
}
