using IronCore.API.DTOs;
using IronCore.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IronCore.API.Controllers;

[Route("api/members")]
[Authorize]
public class MembersController(IMemberService memberService) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> GetAll() =>
        Ok(await memberService.GetAllAsync(GymId));

    [HttpGet("my-clients")]
    [Authorize(Policy = "TrainerOnly")]
    public async Task<IActionResult> GetMyClients() =>
        Ok(await memberService.GetByTrainerAsync(UserId));

    [HttpGet("me")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> GetMe()
    {
        var profile = await memberService.GetByUserIdAsync(UserId);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpPost]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Create([FromBody] CreateMemberRequest req)
    {
        var (member, err) = await memberService.CreateDirectlyAsync(GymId, req);
        return err != null ? BadRequest(new { error = err }) : Ok(member);
    }

    [HttpPost("assign-trainer")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> AssignTrainer([FromBody] AssignTrainerRequest req)
    {
        var (success, err) = await memberService.AssignTrainerAsync(req);
        return err != null ? BadRequest(new { error = err }) : Ok(new { message = "Trainer assigned." });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "OwnerOrTrainer")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateMemberRequest req)
    {
        var (success, err) = await memberService.UpdateAsync(id, req);
        return err != null ? NotFound(new { error = err }) : Ok(new { message = "Updated." });
    }

    [HttpPut("{id}/password")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> UpdatePassword(string id, [FromBody] UpdatePasswordRequest req)
    {
        var (success, err) = await memberService.UpdatePasswordAsync(id, req.NewPassword);
        return err != null ? BadRequest(new { error = err }) : Ok(new { message = "Password updated." });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Delete(string id)
    {
        var (success, err) = await memberService.DeleteAsync(id);
        return err != null ? NotFound(new { error = err }) : Ok(new { message = "Member deactivated." });
    }
}
