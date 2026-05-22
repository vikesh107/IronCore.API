using IronCore.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IronCore.API.Controllers;

[Route("api/dashboard")]
[Authorize]
public class DashboardController(IDashboardService dashboardService) : BaseController
{
    [HttpGet("owner")]
    [Authorize(Policy = "OwnerOnly")]
    public async Task<IActionResult> OwnerDashboard() =>
        Ok(await dashboardService.GetOwnerDashboardAsync(GymId));

    [HttpGet("trainer")]
    [Authorize(Policy = "TrainerOnly")]
    public async Task<IActionResult> TrainerDashboard() =>
        Ok(await dashboardService.GetTrainerDashboardAsync(UserId));

    [HttpGet("member")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> MemberDashboard() =>
        Ok(await dashboardService.GetMemberDashboardAsync(UserId));
}
