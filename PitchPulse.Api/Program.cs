using PitchPulse.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<SeriesStore>();
builder.Services.AddSingleton<MeasurementStore>();

var app = builder.Build();

app.MapControllers();

app.Run();
