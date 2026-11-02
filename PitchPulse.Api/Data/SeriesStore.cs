using PitchPulse.Api.Models;

namespace PitchPulse.Api.Data;

public class SeriesStore
{
    private readonly List<MetricSeries> _series = new()
    {
        new MetricSeries
        {
            Id = 1,
            Name = "Tętno — zawodnik nr 7",
            PlayerId = 1,
            Player = new Player
            {
                Id = 1,
                FirstName = "Adam",
                LastName = "Nowak",
                JerseyNumber = 7
            },
            MetricType = MetricType.HeartRate,
            MinValue = 40,
            MaxValue = 220,
            Color = "#1f77b4",
            Icon = "circle",
            Unit = "bpm"
        },
        new MetricSeries
        {
            Id = 2,
            Name = "Tętno — zawodnik nr 9",
            PlayerId = 2,
            Player = new Player
            {
                Id = 2,
                FirstName = "Michał",
                LastName = "Kowalski",
                JerseyNumber = 9
            },
            MetricType = MetricType.HeartRate,
            MinValue = 40,
            MaxValue = 220,
            Color = "#ff7f0e",
            Icon = "square",
            Unit = "bpm"
        },
        new MetricSeries
        {
            Id = 3,
            Name = "Tętno — zawodnik nr 10",
            PlayerId = 3,
            Player = new Player
            {
                Id = 3,
                FirstName = "Piotr",
                LastName = "Wiśniewski",
                JerseyNumber = 10
            },
            MetricType = MetricType.HeartRate,
            MinValue = 40,
            MaxValue = 220,
            Color = "#2ca02c",
            Icon = "diamond",
            Unit = "bpm"
        }
    };

    public IReadOnlyList<MetricSeries> GetAll()
    {
        return _series;
    }

    public MetricSeries? GetById(int id)
    {
        return _series.FirstOrDefault(series => series.Id == id);
    }
}
