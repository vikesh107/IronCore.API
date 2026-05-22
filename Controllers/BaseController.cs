using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IronCore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase
{
    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    protected string UserRole => User.FindFirstValue(ClaimTypes.Role)!;
    protected string GymId => User.FindFirstValue("gymId")!;
}
