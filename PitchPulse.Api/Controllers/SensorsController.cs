using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Data;

namespace PitchPulse.Api.Controllers;

[ApiController]
[Route("api/sensors")]
[Authorize(Roles = "Administrator")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SensorsController(SensorStore sensorStore) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SensorResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var sensors = await sensorStore.GetAllAsync(cancellationToken);
        return Ok(sensors.Select(SensorResponse.FromModel).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SensorResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var sensor = await sensorStore.GetByIdAsync(id, cancellationToken);
        return sensor is null ? SensorNotFound() : Ok(SensorResponse.FromModel(sensor));
    }

    [HttpPost]
    public async Task<ActionResult<SensorCreatedResponse>> Register(SensorRequest request, CancellationToken cancellationToken)
    {
        var sensor = await sensorStore.RegisterAsync(request, cancellationToken);
        if (sensor is null)
        {
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Nie znaleziono serii dla czujnika.");
        }

        return CreatedAtAction(nameof(GetById), new { id = sensor.Id }, sensor);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var removed = await sensorStore.DeleteAsync(id, cancellationToken);
        return removed ? NoContent() : SensorNotFound();
    }

    private ObjectResult SensorNotFound()
    {
        return Problem(statusCode: StatusCodes.Status404NotFound, title: "Nie znaleziono czujnika.");
    }
}
