using IronCore.API.DTOs;
using IronCore.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IronCore.API.Controllers;

[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController(ISubscriptionService subscriptionService) : BaseController
{
    [HttpGet("member/{memberId}")]
    [Authorize(Policy = "OwnerOrTrainer")]
    public async Task<IActionResult> GetByMember(string memberId) =>
        Ok(await subscriptionService.GetByMemberAsync(memberId));

    [HttpPost]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest req)
    {
        var (sub, err) = await subscriptionService.CreateAsync(req);
        return err != null ? BadRequest(new { error = err }) : Ok(sub);
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateSubscriptionStatusRequest req)
    {
        var (success, err) = await subscriptionService.UpdateStatusAsync(id, req.Status);
        return err != null ? BadRequest(new { error = err }) : Ok(new { message = "Status updated." });
    }

    [HttpPatch("{id}/dates")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> UpdateDates(string id, [FromBody] UpdateSubscriptionDatesRequest req)
    {
        var (success, err) = await subscriptionService.UpdateDatesAsync(id, req.StartDate, req.EndDate);
        return err != null ? BadRequest(new { error = err }) : Ok(new { message = "Dates updated." });
    }
}
