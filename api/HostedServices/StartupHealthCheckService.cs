using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using api.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace api.HostedServices
{
    /// <summary>
    /// Hosted service that performs startup tasks including storage connection testing
    /// </summary>
    public class StartupHealthCheckService : IHostedService
    {
        private readonly StorageService _storageService;
        private readonly ILogger<StartupHealthCheckService> _logger;

        public StartupHealthCheckService(StorageService storageService, ILogger<StartupHealthCheckService> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting application health checks...");

            try
            {
                // Test Azure Storage connection
                var storageConnectionTest = await _storageService.TestConnectionAsync();

                if (storageConnectionTest)
                {
                    _logger.LogInformation("✅ Azure Storage connection test passed");
                }
                else
                {
                    _logger.LogWarning("⚠️ Azure Storage connection test failed - application may not function correctly");
                }

                // Log overall startup status
                _logger.LogInformation("Application health checks completed. Storage: {StorageStatus}",
                    storageConnectionTest ? "Healthy" : "Unhealthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during startup health checks: {ErrorMessage}", ex.Message);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping startup health check service...");
            return Task.CompletedTask;
        }
    }
}
