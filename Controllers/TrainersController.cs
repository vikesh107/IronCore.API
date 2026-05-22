using IronCore.API.DTOs;
using IronCore.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IronCore.API.Controllers;

[Route("api/trainers")]
[Authorize]
public class TrainersController(ITrainerService trainerService) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> GetAll() =>
        Ok(await trainerService.GetAllAsync(GymId));

    [HttpGet("{id}")]
    [Authorize(Policy = "OwnerOrTrainer")]
    public async Task<IActionResult> GetById(string id)
    {
        var trainer = await trainerService.GetByIdAsync(id);
        return trainer == null ? NotFound() : Ok(trainer);
    }

    [HttpPost]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Create([FromBody] CreateTrainerRequest req)
    {
        var (trainer, err) = await trainerService.CreateDirectlyAsync(GymId, req);
        return err != null ? BadRequest(new { error = err }) : Ok(trainer);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "OwnerOrTrainer")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateTrainerRequest req)
    {
        var (success, err) = await trainerService.UpdateAsync(id, req);
        return err != null ? NotFound(new { error = err }) : Ok(new { message = "Updated." });
    }

    [HttpPut("{id}/password")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> UpdatePassword(string id, [FromBody] UpdatePasswordRequest req)
    {
        var (success, err) = await trainerService.UpdatePasswordAsync(id, req.NewPassword);
        return err != null ? BadRequest(new { error = err }) : Ok(new { message = "Password updated." });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Delete(string id)
    {
        var (success, err) = await trainerService.DeleteAsync(id);
        return err != null ? NotFound(new { error = err }) : Ok(new { message = "Trainer removed." });
    }
}
