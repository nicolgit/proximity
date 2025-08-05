using Azure.Storage.Blobs;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Globalization;
using Generator.Types;
using Generator.Managers;

namespace Generator;

public class Program
{
    private static ILogger<Program>? _logger;
    private static IConfiguration? _configuration;

    public static async Task<int> Main(string[] args)
    {
        // Create root command
        var rootCommand = new RootCommand("Metro Proximity Generator - Manage areas and generate station proximity data");

        // Define logging option (available for all commands)
        var loggingOption = new Option<string>(
            aliases: new[] { "--logging", "-l" },
            description: "Set the minimum log level (Trace, Debug, Information, Warning, Error, Critical, None)")
        {
            ArgumentHelpName = "level"
        };
        loggingOption.SetDefaultValue("None");

        // Add area command group
        var areaCommand = new Command("area", "Manage areas for proximity calculations");

        // Create area create command
        var createCommand = new Command("create", "Create a new area");

        var nameArgument = new Argument<string>("name", "The name of the area");
        var centerOption = new Option<string>(
            aliases: new[] { "--center" },
            description: "Center coordinates as 'latitude,longitude'")
        {
            IsRequired = true,
            ArgumentHelpName = "lat,lon"
        };
        var diameterOption = new Option<int>(
            aliases: new[] { "--diameter" },
            description: "Diameter in meters")
        {
            IsRequired = true,
            ArgumentHelpName = "meters"
        };
        var displayNameOption = new Option<string>(
            aliases: new[] { "--displayname" },
            description: "Display name for the area")
        {
            IsRequired = true,
            ArgumentHelpName = "display_name"
        };
        var developerOption = new Option<bool>(
            aliases: new[] { "--developer" },
            description: "Developer mode: limit to first 3 stations and 3 tram stops")
        {
            ArgumentHelpName = "developer"
        };

        createCommand.AddArgument(nameArgument);
        createCommand.AddOption(centerOption);
        createCommand.AddOption(diameterOption);
        createCommand.AddOption(displayNameOption);
        createCommand.AddOption(developerOption);
        createCommand.AddOption(loggingOption);

        createCommand.SetHandler(async (string name, string center, int diameter, string displayName, bool developer, string loggingLevel) =>
        {
            await InitializeLoggingAndConfigurationAsync(loggingLevel);
            await AreaManager.CreateAreaAsync(name, center, diameter, displayName, developer, _logger, _configuration);
        }, nameArgument, centerOption, diameterOption, displayNameOption, developerOption, loggingOption);

        // Create area delete command
        var deleteCommand = new Command("delete", "Delete an area");
        var deleteNameArgument = new Argument<string>("name", "The name of the area to delete");

        deleteCommand.AddArgument(deleteNameArgument);
        deleteCommand.AddOption(loggingOption);

        deleteCommand.SetHandler(async (string name, string loggingLevel) =>
        {
            await InitializeLoggingAndConfigurationAsync(loggingLevel);
            await DeleteAreaAsync(name);
        }, deleteNameArgument, loggingOption);

        // Add commands to area group
        areaCommand.AddCommand(createCommand);
        areaCommand.AddCommand(deleteCommand);

        // Add commands to root
        rootCommand.AddCommand(areaCommand);

        // Parse and invoke the command
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task InitializeLoggingAndConfigurationAsync(string loggingLevel)
    {
        // Parse logging level
        if (!Enum.TryParse<LogLevel>(loggingLevel, true, out var logLevel))
        {
            Console.WriteLine($"Invalid logging level: {loggingLevel}");
            Console.WriteLine("Valid levels: Trace, Debug, Information, Warning, Error, Critical, None");
            Environment.Exit(1);
            return;
        }

        // Configure logging with the specified level
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(logLevel));
        _logger = loggerFactory.CreateLogger<Program>();

        _logger.LogInformation("Log level set to: {LogLevel}", logLevel);

        // Load configuration
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("generator.config.json", optional: false, reloadOnChange: true)
            .Build();

        _logger.LogInformation("Configuration loaded successfully");

        // Test Azure Storage and map connection 
        var storageTestPassed = await TestAzureStorageConnectionAsync();
        var mapApiTestPassed = await TestMapBoxApiKeyAsync();

        // Exit if any critical tests failed
        if (!storageTestPassed || !mapApiTestPassed)
        {
            _logger?.LogError("One or more critical service tests failed. Exiting application.");
            Console.WriteLine("❌ Critical service validation failed. Please check your configuration.");
            Environment.Exit(1);
        }

        // Add a small delay to make this truly async
        await Task.Delay(1);
    }

    private static async Task DeleteAreaAsync(string name)
    {
        try
        {
            _logger?.LogInformation("Deleting area: {Name}", name);

            // Get Azure Storage connection
            var connectionString = _configuration?.GetConnectionString("AzureStorage");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("❌ Azure Storage connection string not configured");
                Environment.Exit(1);
                return;
            }

            // Connect to Azure Table Storage
            var tableServiceClient = new TableServiceClient(connectionString);
            var tableClient = tableServiceClient.GetTableClient("area");

            // Try to get the entity first to check if it exists
            try
            {
                var response = await tableClient.GetEntityAsync<AreaEntity>("area", name.ToLowerInvariant());
                var existingArea = response.Value;

                // Delete the entity
                await tableClient.DeleteEntityAsync("area", name.ToLowerInvariant());

                _logger?.LogInformation("Area deleted successfully: {Name}", name);
                Console.WriteLine($"✓ Area '{name}' deleted successfully!");
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger?.LogWarning("Area not found: {Name}", name);
                Console.WriteLine($"❌ Area '{name}' not found");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete area: {Name}", name);
            Console.WriteLine($"❌ Failed to delete area '{name}': {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task<bool> TestAzureStorageConnectionAsync()
    {
        try
        {
            var connectionString = _configuration?.GetConnectionString("AzureStorage");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger?.LogInformation("No Azure Storage connection string configured. Skipping storage test.");
                return true; // Not configured is not a failure
            }

            _logger?.LogInformation("Testing Azure Storage connection...");

            // Create BlobServiceClient with connection string
            var blobServiceClient = new BlobServiceClient(connectionString);

            // Test connection by getting account info
            var accountInfo = await blobServiceClient.GetAccountInfoAsync();

            _logger?.LogInformation("Successfully connected to Azure Storage Account");
            _logger?.LogInformation("Account Kind: {AccountKind}", accountInfo.Value.AccountKind);
            _logger?.LogInformation("SKU Name: {SkuName}", accountInfo.Value.SkuName);

            Console.WriteLine($"✓ Azure Storage connection test successful!");
            Console.WriteLine($"  Account Kind: {accountInfo.Value.AccountKind}");
            Console.WriteLine($"  SKU Name: {accountInfo.Value.SkuName}");
            Console.WriteLine();
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to Azure Storage. This is a critical error.");
            Console.WriteLine("❌ Azure Storage connection test failed (check configuration)");
            Console.WriteLine($"   Error: {ex.Message}");
            Console.WriteLine();
            return false;
        }
    }

    private static async Task<bool> TestMapBoxApiKeyAsync()
    {
        try
        {
            var mapBoxKey = _configuration?.GetSection("AppSettings")["mapBoxSubscriptionKey"];

            if (string.IsNullOrWhiteSpace(mapBoxKey) || mapBoxKey.Contains("<") || mapBoxKey.Contains(">"))
            {
                _logger?.LogInformation("No valid MapBox API key configured. Skipping MapBox API test.");
                return false;
            }

            _logger?.LogInformation("Testing MapBox API key...");

            // Test MapBox API with a simple account validation request
            // Using the MapBox Account API to validate the token
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var url = $"https://api.mapbox.com/tokens/v2?access_token={mapBoxKey}";

            _logger?.LogDebug("Calling MapBox API: {Url}", url.Replace(mapBoxKey, "***"));

            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger?.LogInformation("Successfully validated MapBox API key");
                _logger?.LogDebug("MapBox API response: {Response}", content);

                Console.WriteLine($"✓ MapBox API key validation successful!");
                Console.WriteLine($"  Status: {response.StatusCode}");
                Console.WriteLine();
                return true;
            }
            else
            {
                _logger?.LogError("MapBox API key validation failed with status: {StatusCode}", response.StatusCode);
                Console.WriteLine($"❌ MapBox API key validation failed (Status: {response.StatusCode})");
                Console.WriteLine();
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to connect to MapBox API. Check internet connection and API key.");
            Console.WriteLine("❌ MapBox API connection test failed (check network/key)");
            Console.WriteLine($"   Error: {ex.Message}");
            Console.WriteLine();
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogError(ex, "MapBox API request timed out.");
            Console.WriteLine("❌ MapBox API request timed out");
            Console.WriteLine($"   Error: {ex.Message}");
            Console.WriteLine();
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to validate MapBox API key. This is a critical error.");
            Console.WriteLine("❌ MapBox API key validation failed (check configuration)");
            Console.WriteLine($"   Error: {ex.Message}");
            Console.WriteLine();
            return false;
        }
    }
}
