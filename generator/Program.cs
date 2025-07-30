using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Generator;

public class Program
{
    private static ILogger<Program>? _logger;
    private static IConfiguration? _configuration;

    public static async Task Main(string[] args)
    {
        // Configure logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<Program>();

        try
        {
            _logger.LogInformation("Generator Application Starting...");

            // Load configuration
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("generator.config.json", optional: false, reloadOnChange: true)
                .Build();

            _logger.LogInformation("Configuration loaded successfully");

            // Display Hello World message
            Console.WriteLine("Hello World from metro-proximity generator!");
            Console.WriteLine("=================================");

            // Test Azure Storage connection (optional - only if connection string is provided)
            await TestAzureStorageConnectionAsync();

            _logger.LogInformation("Generator Application Completed Successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "An error occurred while running the generator application");
            Environment.Exit(1);
        }
    }

    private static async Task TestAzureStorageConnectionAsync()
    {
        try
        {
            var connectionString = _configuration?.GetConnectionString("AzureStorage");
            
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger?.LogInformation("No Azure Storage connection string configured. Skipping storage test.");
                return;
            }

            _logger?.LogInformation("Testing Azure Storage connection...");

            // Create BlobServiceClient with connection string
            // Note: In production, prefer using Managed Identity over connection strings
            var blobServiceClient = new BlobServiceClient(connectionString);

            // Test connection by getting account info
            var accountInfo = await blobServiceClient.GetAccountInfoAsync();
            
            _logger?.LogInformation("Successfully connected to Azure Storage Account");
            _logger?.LogInformation("Account Kind: {AccountKind}", accountInfo.Value.AccountKind);
            _logger?.LogInformation("SKU Name: {SkuName}", accountInfo.Value.SkuName);

            Console.WriteLine($"✓ Azure Storage connection test successful!");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to connect to Azure Storage. This is expected if no valid connection string is provided.");
            Console.WriteLine("⚠ Azure Storage connection test failed (check configuration)");
        }
    }
}
