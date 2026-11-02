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
    public ActionResult<IReadOnlyList<SeriesResponse>> GetAll()
    {
        var response = _seriesStore.GetAll()
            .Select(SeriesResponse.FromModel)
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public ActionResult<SeriesResponse> GetById(int id)
    {
        var series = _seriesStore.GetById(id);

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
