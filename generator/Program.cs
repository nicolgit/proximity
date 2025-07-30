using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace Generator;

public class Program
{
    private static ILogger<Program>? _logger;
    private static IConfiguration? _configuration;

    public static async Task<int> Main(string[] args)
    {
        // Define command-line options
        var loggingOption = new Option<string>(
            aliases: new[] { "--logging", "-l" },
            description: "Set the minimum log level (Trace, Debug, Information, Warning, Error, Critical, None)")
        {
            ArgumentHelpName = "level"
        };
        loggingOption.SetDefaultValue("Information");

        var helpOption = new Option<bool>(
            aliases: new[] { "--help", "-h", "-?" },
            description: "Show help information");

        // Create root command
        var rootCommand = new RootCommand("Metro Proximity Generator - Generate station proximity data using Azure Maps or MapBox APIs")
        {
            loggingOption,
            helpOption
        };

        // Set the handler for the root command
        rootCommand.SetHandler(async (loggingLevel, showHelp) =>
        {
            if (showHelp)
            {
                ShowDetailedHelp();
                return;
            }

            await RunGeneratorAsync(loggingLevel);
        }, loggingOption, helpOption);

        // Parse and invoke the command
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunGeneratorAsync(string loggingLevel)
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

        try
        {
            _logger.LogInformation("Metro Proximity Generator Starting...");
            _logger.LogInformation("Log level set to: {LogLevel}", logLevel);

            // Load configuration
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("generator.config.json", optional: false, reloadOnChange: true)
                .Build();

            _logger.LogInformation("Configuration loaded successfully");

            // Display startup message
            Console.WriteLine("Metro Proximity Generator");
            Console.WriteLine("=========================");
            Console.WriteLine($"Log Level: {logLevel}");
            Console.WriteLine();

            // Test Azure Storage and map connection 
            var storageTestPassed = await TestAzureStorageConnectionAsync();
            var mapApiTestPassed = await TestMapBoxApiKeyAsync();

            // Exit if any critical tests failed
            if (!storageTestPassed || !mapApiTestPassed)
            {
                _logger?.LogError("One or more critical service tests failed. Exiting application.");
                Console.WriteLine("❌ Critical service validation failed. Please check your configuration.");
                Environment.Exit(1);
                return;
            }

            _logger.LogInformation("Metro Proximity Generator Completed Successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "An error occurred while running the generator application");
            Environment.Exit(1);
        }
    }

    private static void ShowDetailedHelp()
    {
        Console.WriteLine("Metro Proximity Generator");
        Console.WriteLine("=========================");
        Console.WriteLine();
        Console.WriteLine("DESCRIPTION:");
        Console.WriteLine("    Generates station proximity data for metro systems using Azure Maps or MapBox APIs.");
        Console.WriteLine("    The application reads configuration from generator.config.json and outputs");
        Console.WriteLine("    proximity polygons for each metro station at specified distances.");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("    generator [options]");
        Console.WriteLine();
        Console.WriteLine("OPTIONS:");
        Console.WriteLine("    -l, --logging <level>    Set the minimum log level");
        Console.WriteLine("                            Valid values: Trace, Debug, Information, Warning, Error, Critical, None");
        Console.WriteLine("                            Default: Information");
        Console.WriteLine();
        Console.WriteLine("    -h, --help              Show this help information");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("    generator                           # Run with default Information logging");
        Console.WriteLine("    generator --logging Debug           # Run with Debug logging level");
        Console.WriteLine("    generator -l Warning                # Run with Warning logging level");
        Console.WriteLine("    generator --help                    # Show this help");
        Console.WriteLine();
        Console.WriteLine("CONFIGURATION:");
        Console.WriteLine("    The application requires a generator.config.json file with:");
        Console.WriteLine("    - Azure Storage connection string (for output storage)");
        Console.WriteLine("    - API configuration (Azure Maps or MapBox)");
        Console.WriteLine("    - Metro line definitions");
        Console.WriteLine("    - Distance parameters");
        Console.WriteLine();
        Console.WriteLine("SECURITY NOTES:");
        Console.WriteLine("    - Use Azure Managed Identity in production instead of connection strings");
        Console.WriteLine("    - Store sensitive keys in Azure Key Vault");
        Console.WriteLine("    - Never commit real connection strings to source control");
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
            // Note: In production, prefer using Managed Identity over connection strings
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
                return true; // Not configured is not a failure
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
