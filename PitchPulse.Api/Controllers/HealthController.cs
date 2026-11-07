using Microsoft.AspNetCore.Mvc;
using PitchPulse.Api.Data;

namespace PitchPulse.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(PitchPulseDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Baza danych jest niedostępna.");
        }

        return Ok(new { status = "ok" });
    }
}
