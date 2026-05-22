using IronCore.API.DTOs;
using IronCore.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IronCore.API.Controllers;

[Route("api/attendance")]
[Authorize(Policy = "TrainerOnly")]
public class AttendanceController(IAttendanceService attendanceService) : BaseController
{
    // GET /api/attendance/member/{memberId}
    [HttpGet("member/{memberId}")]
    public async Task<IActionResult> GetByMember(string memberId) =>
        Ok(await attendanceService.GetByMemberAsync(UserId, memberId));

    // GET /api/attendance/date/{date}  e.g. 2026-05-23
    [HttpGet("date/{date}")]
    public async Task<IActionResult> GetByDate(DateOnly date) =>
        Ok(await attendanceService.GetByDateAsync(UserId, date));

    // POST /api/attendance
    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] CreateAttendanceLogRequest req)
    {
        var (log, err) = await attendanceService.UpsertAsync(UserId, req);
        return err != null ? BadRequest(new { error = err }) : Ok(log);
    }
}
