using Microsoft.EntityFrameworkCore;
using PitchPulse.Api.Models;

namespace PitchPulse.Api.Data;

public class PitchPulseDbContext : DbContext
{
    public PitchPulseDbContext(DbContextOptions<PitchPulseDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<MetricSeries> Series => Set<MetricSeries>();
    public DbSet<Measurement> Measurements => Set<Measurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetricSeries>(entity =>
        {
            entity.ToTable("Series", table =>
                table.HasCheckConstraint("CK_Series_ValueRange", "\"MinValue\" < \"MaxValue\""));

            entity.Property(series => series.Color).HasMaxLength(7);

            entity.HasOne(series => series.Player)
                .WithMany()
                .HasForeignKey(series => series.PlayerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Measurement>(entity =>
        {
            entity.HasOne<MetricSeries>()
                .WithMany()
                .HasForeignKey(measurement => measurement.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(measurement => measurement.Timestamp);
            entity.HasIndex(measurement => new { measurement.SeriesId, measurement.Timestamp });
        });

        modelBuilder.Entity<Player>().HasData(
            new Player { Id = 1, FirstName = "Adam", LastName = "Nowak", JerseyNumber = 7 },
            new Player { Id = 2, FirstName = "Michał", LastName = "Kowalski", JerseyNumber = 9 },
            new Player { Id = 3, FirstName = "Piotr", LastName = "Wiśniewski", JerseyNumber = 10 });

        modelBuilder.Entity<MetricSeries>().HasData(
            new MetricSeries
            {
                Id = 1,
                Name = "Tętno — zawodnik nr 7",
                PlayerId = 1,
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
                MetricType = MetricType.HeartRate,
                MinValue = 40,
                MaxValue = 220,
                Color = "#2ca02c",
                Icon = "diamond",
                Unit = "bpm"
            });
    }
}
