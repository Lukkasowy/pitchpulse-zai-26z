using Microsoft.AspNetCore.Authorization;
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

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SeriesResponse>> Create(SeriesRequest request, CancellationToken cancellationToken)
    {
        var result = await _seriesStore.CreateAsync(request, cancellationToken);
        if (result.Error != SeriesWriteError.None)
        {
            return WriteProblem(result.Error);
        }

        var response = SeriesResponse.FromModel(result.Series!);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SeriesResponse>> Update(int id, SeriesRequest request, CancellationToken cancellationToken)
    {
        var result = await _seriesStore.UpdateAsync(id, request, cancellationToken);
        if (result.Error != SeriesWriteError.None)
        {
            return WriteProblem(result.Error);
        }

        return Ok(SeriesResponse.FromModel(result.Series!));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var removed = await _seriesStore.DeleteAsync(id, cancellationToken);
        return removed ? NoContent() : WriteProblem(SeriesWriteError.NotFound);
    }

    private ObjectResult WriteProblem(SeriesWriteError error)
    {
        return error switch
        {
            SeriesWriteError.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Nie znaleziono serii."),
            SeriesWriteError.InvalidRange => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Niepoprawny zakres wartości.",
                detail: "Podaj skończone liczby. Minimum musi być mniejsze od maksimum."),
            SeriesWriteError.InvalidPlayer => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Nie znaleziono zawodnika.",
                detail: "Wybierz istniejącego zawodnika albo pomiń playerId."),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Zakres wyklucza zapisane pomiary.",
                detail: "Nowy przedział musi obejmować wszystkie wartości zapisane w tej serii.")
        };
    }
}
