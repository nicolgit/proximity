using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using api.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure Application Insights
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register custom services
builder.Services.AddSingleton<StorageService>();
builder.Services.AddSingleton<AreaService>();
builder.Services.AddSingleton<StationService>();
builder.Services.AddSingleton<CdnResponseService>();

builder.Build().Run();
