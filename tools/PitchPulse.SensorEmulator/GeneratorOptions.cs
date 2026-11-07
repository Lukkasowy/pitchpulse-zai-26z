using System.Globalization;
using System.Text.RegularExpressions;

namespace PitchPulse.SensorEmulator;

internal record GeneratorOptions(Uri Api, string Key, string Pattern, double Min, double Max, int? Count, int IntervalSeconds, DateTimeOffset? Start)
{
    public static GeneratorOptions Parse(string[] args)
    {
        var options = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length || args[i] is not ("--api" or "--key" or "--mode" or "--pattern" or
                "--min" or "--max" or "--value" or "--count" or "--interval-seconds" or "--start") ||
                !options.TryAdd(args[i], args[i + 1]))
            {
                throw new ArgumentException("Podaj opcje jako pary nazwa–wartość i użyj każdej opcji tylko raz.");
            }
        }

        if (!Uri.TryCreate(options.GetValueOrDefault("--api"), UriKind.Absolute, out var api) ||
            api.Scheme is not ("http" or "https") || api.UserInfo != "" || api.AbsolutePath != "/" || api.Query != "" || api.Fragment != "")
        {
            throw new ArgumentException("Podaj adres bazowy w --api, np. http://localhost:5090.");
        }

        var key = (options.GetValueOrDefault("--key") ?? Environment.GetEnvironmentVariable("PITCHPULSE_SENSOR_KEY"))?.Trim();
        if (string.IsNullOrEmpty(key) || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl))
        {
            throw new ArgumentException("Podaj klucz w PITCHPULSE_SENSOR_KEY lub w opcji --key.");
        }

        var pattern = options.GetValueOrDefault("--pattern", "random");
        double min;
        double max;
        if (pattern == "constant" && !options.ContainsKey("--min") && !options.ContainsKey("--max"))
        {
            min = max = ReadNumber(options, "--value");
        }
        else if (pattern is "random" or "sine" && !options.ContainsKey("--value"))
        {
            min = ReadNumber(options, "--min");
            max = ReadNumber(options, "--max");
            if (min >= max)
            {
                throw new ArgumentException("--min musi być mniejsze niż --max.");
            }
        }
        else
        {
            throw new ArgumentException("Wybierz random lub sine z --min i --max albo constant z --value.");
        }

        var mode = options.GetValueOrDefault("--mode", "live");
        int? count = options.TryGetValue("--count", out var countText)
            ? ReadPositiveInt(countText, "--count")
            : null;
        var interval = ReadPositiveInt(options.GetValueOrDefault("--interval-seconds", "1"), "--interval-seconds");
        if (interval > 86400)
        {
            throw new ArgumentException("--interval-seconds może wynosić maksymalnie 86400.");
        }

        if (mode == "live" && !options.ContainsKey("--start"))
        {
            return new GeneratorOptions(api, key, pattern, min, max, count, interval, null);
        }

        if (mode != "history" || !count.HasValue || !options.TryGetValue("--start", out var startText))
        {
            throw new ArgumentException("Użyj trybu live bez --start albo history z --start i --count.");
        }

        if (!Regex.IsMatch(startText, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$") ||
            !DateTimeOffset.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
        {
            throw new ArgumentException("Podaj --start jako datę ISO 8601 ze strefą, np. 2026-11-06T18:30:00Z.");
        }

        var durationSeconds = (long)(count.Value - 1) * interval;
        if (durationSeconds > (DateTimeOffset.UtcNow - start).TotalSeconds)
        {
            throw new ArgumentException("Ostatni pomiar w trybie historycznym nie może wypadać w przyszłości.");
        }

        return new GeneratorOptions(api, key, pattern, min, max, count, interval, start.ToUniversalTime());
    }

    private static double ReadNumber(Dictionary<string, string> options, string name)
    {
        if (!double.TryParse(options.GetValueOrDefault(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
        {
            throw new ArgumentException($"Podaj skończoną liczbę w {name}. Użyj kropki dziesiętnej.");
        }

        return value;
    }

    private static int ReadPositiveInt(string text, string name)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1)
        {
            throw new ArgumentException($"Podaj dodatnią liczbę całkowitą w {name}.");
        }

        return value;
    }
}
