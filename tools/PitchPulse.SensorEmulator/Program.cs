using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PitchPulse.SensorEmulator;

const string help = """
    Emulator czujnika PitchPulse

    --api URL                Adres bazowy aplikacji, np. http://localhost:5090
    --key KLUCZ              Alternatywa dla zmiennej PITCHPULSE_SENSOR_KEY
    --mode live|history      Domyślnie live
    --pattern random|sine|constant  Domyślnie random
    --min LICZBA --max LICZBA      Zakres dla random i sine
    --value LICZBA                 Stały wynik dla constant
    --count LICZBA           Liczba pomiarów. W live można pominąć i zakończyć przez Ctrl+C
    --interval-seconds LICZBA      Odstęp lub krok czasu, domyślnie 1 sekunda
    --start DATA             Początek dla history, ISO 8601 ze strefą czasową
    """;

if (args.Length == 0 || args is ["--help"])
{
    Console.WriteLine(help);
    return 0;
}

GeneratorOptions options;
try
{
    options = GeneratorOptions.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Lista opcji: --help");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
client.DefaultRequestHeaders.Add("X-API-Key", options.Key);
var endpoint = new Uri(options.Api, "/api/measurements");
using var timer = options.Start.HasValue ? null : new PeriodicTimer(TimeSpan.FromSeconds(options.IntervalSeconds));
long accepted = 0;
long rejected = 0;

try
{
    for (long index = 0; !options.Count.HasValue || index < options.Count.Value; index++)
    {
        if (timer is not null && index > 0)
        {
            await timer.WaitForNextTickAsync(cancellation.Token);
        }

        var fraction = options.Pattern switch
        {
            "sine" => (1 + Math.Sin(2 * Math.PI * (index % 20) / 20)) / 2,
            "random" => Random.Shared.NextDouble(),
            _ => 0
        };
        var value = Math.Clamp(options.Min * (1 - fraction) + options.Max * fraction, options.Min, options.Max);
        var timestamp = options.Start?.AddSeconds(index * options.IntervalSeconds);
        using var response = await client.PostAsJsonAsync(endpoint, new MeasurementInput(value, timestamp), cancellation.Token);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            accepted++;
            Console.WriteLine($"{index + 1}. Zapisano pomiar {value:G6}{(timestamp.HasValue ? $" ({timestamp:O})" : "")}");
            continue;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Console.Error.WriteLine("API odrzuciło klucz czujnika (401). Zakończono wysyłanie.");
            return 1;
        }

        if (response.StatusCode != HttpStatusCode.UnprocessableEntity)
        {
            Console.Error.WriteLine($"API zwróciło HTTP {(int)response.StatusCode}. Zakończono wysyłanie.");
            return 1;
        }

        rejected++;
        var title = await ReadProblemTitleAsync(response, cancellation.Token);
        Console.Error.WriteLine($"{index + 1}. Odrzucono wartość {value:G6} (422). {title}");
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("Zatrzymano emulator.");
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Przekroczono czas oczekiwania na odpowiedź API.");
    return 1;
}
catch (HttpRequestException)
{
    Console.Error.WriteLine("Nie udało się połączyć z API. Sprawdź adres i czy aplikacja jest uruchomiona.");
    return 1;
}

Console.WriteLine($"Zapisano {accepted}, odrzucono {rejected}.");
return rejected == 0 ? 0 : 1;

static async Task<string> ReadProblemTitleAsync(HttpResponseMessage response, CancellationToken cancellationToken)
{
    try
    {
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken);
        return problem?.Title ?? "";
    }
    catch (JsonException)
    {
        return "";
    }
}

internal record MeasurementInput(double Value, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? Timestamp);
internal record ApiProblem(string? Title);
