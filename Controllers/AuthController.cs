using IronCore.API.DTOs;
using IronCore.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IronCore.API.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register-owner")]
    public async Task<IActionResult> RegisterOwner([FromBody] RegisterOwnerRequest req)
    {
        var (res, err) = await authService.RegisterOwnerAsync(req);
        return err != null ? BadRequest(new { error = err }) : Ok(res);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var (res, err) = await authService.LoginAsync(req);
        return err != null ? Unauthorized(new { error = err }) : Ok(res);
    }

    [HttpGet("gym-info")]
    public async Task<IActionResult> GetGymInfo()
    {
        var info = await authService.GetGymInfoAsync();
        return info == null ? NotFound(new { error = "No gym registered yet." }) : Ok(new { id = info.Value.Id, name = info.Value.Name });
    }

    [HttpPost("register-member")]
    public async Task<IActionResult> RegisterMember([FromBody] RegisterMemberRequest req)
    {
        var (res, err) = await authService.RegisterMemberAsync(req);
        return err != null ? BadRequest(new { error = err }) : Ok(res);
    }

    [HttpGet("invite-info")]
    public async Task<IActionResult> GetInviteInfo([FromQuery] string token)
    {
        var info = await authService.GetInviteInfoAsync(token);
        return info == null ? NotFound(new { error = "Invalid or expired invite." }) : Ok(info);
    }

    [HttpPost("accept-invite")]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest req)
    {
        var (res, err) = await authService.AcceptInviteAsync(req);
        return err != null ? BadRequest(new { error = err }) : Ok(res);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        await authService.ForgotPasswordAsync(req.Email);
        return Ok(new { message = "If that email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var (success, err) = await authService.ResetPasswordAsync(req);
        return err != null ? BadRequest(new { error = err }) : Ok(new { message = "Password reset successfully." });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest req)
    {
        var (res, err) = await authService.RefreshTokenAsync(req.RefreshToken);
        return err != null ? Unauthorized(new { error = err }) : Ok(res);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await authService.RevokeRefreshTokenAsync(userId);
        return Ok(new { message = "Logged out." });
    }
}
