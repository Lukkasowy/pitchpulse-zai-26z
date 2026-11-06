using Microsoft.AspNetCore.Mvc;
using PitchPulse.Api.Contracts;
using PitchPulse.Api.Data;

namespace PitchPulse.Api.Controllers;

[ApiController]
[Route("api/series")]
public class SeriesController : ControllerBase
{
    private readonly SeriesStore _seriesStore;

    public SeriesController(SeriesStore seriesStore)
    {
        _seriesStore = seriesStore;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeriesResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var series = await _seriesStore.GetAllAsync(cancellationToken);
        var response = series
            .Select(SeriesResponse.FromModel)
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SeriesResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var series = await _seriesStore.GetByIdAsync(id, cancellationToken);

        if (series is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Nie znaleziono serii.",
                detail: $"Seria o identyfikatorze {id} nie istnieje.");
        }

        return Ok(SeriesResponse.FromModel(series));
    }
}
