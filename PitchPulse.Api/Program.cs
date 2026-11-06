using Microsoft.EntityFrameworkCore;
using PitchPulse.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("PitchPulse")
    ?? throw new InvalidOperationException("Brak konfiguracji ConnectionStrings:PitchPulse.");

builder.Services.AddDbContext<PitchPulseDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<SeriesStore>();
builder.Services.AddScoped<MeasurementStore>();

var app = builder.Build();

app.MapControllers();

app.Run();
