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
        var noIsochroneOption = new Option<bool>(
            aliases: new[] { "--noisochrone" },
            description: "Skip isochrone generation when creating the area")
        {
            ArgumentHelpName = "noisochrone"
        };

        createCommand.AddArgument(nameArgument);
        createCommand.AddOption(centerOption);
        createCommand.AddOption(diameterOption);
        createCommand.AddOption(displayNameOption);
        createCommand.AddOption(developerOption);
        createCommand.AddOption(noIsochroneOption);
        createCommand.AddOption(loggingOption);

        createCommand.SetHandler(async (string name, string center, int diameter, string displayName, bool developer, bool noIsochrone, string loggingLevel) =>
        {
            await InitializeLoggingAndConfigurationAsync(loggingLevel);
            await AreaManager.CreateAreaAsync(name, center, diameter, displayName, developer, noIsochrone, _logger, _configuration);
        }, nameArgument, centerOption, diameterOption, displayNameOption, developerOption, noIsochroneOption, loggingOption);

        // Create area delete command
        var deleteCommand = new Command("delete", "Delete an area");
        var deleteNameArgument = new Argument<string>("name", "The name of the area to delete");

        deleteCommand.AddArgument(deleteNameArgument);
        deleteCommand.AddOption(loggingOption);

        deleteCommand.SetHandler(async (string name, string loggingLevel) =>
        {
            await InitializeLoggingAndConfigurationAsync(loggingLevel);
            await AreaManager.DeleteAreaAsync(name, _logger, _configuration);
        }, deleteNameArgument, loggingOption);

        // Create area list command
        var listCommand = new Command("list", "List all areas");

        listCommand.AddOption(loggingOption);

        listCommand.SetHandler(async (string loggingLevel) =>
        {
            await InitializeLoggingAndConfigurationAsync(loggingLevel);
            await AreaManager.ListAreasAsync(_logger, _configuration);
        }, loggingOption);

        // Create area isochrone command
        var areaIsochroneCommand = new Command("isochrone", "Recreate or delete area-wide isochrones using existing station isochrones");
        var areaNameArgument = new Argument<string>("areaname", "The name of the area to regenerate isochrones for");
        var deleteAreaIsochroneOption = new Option<bool>(
            aliases: new[] { "--delete" },
            description: "Delete area-wide isochrones instead of creating them")
        {
            ArgumentHelpName = "delete"
        };

        areaIsochroneCommand.AddArgument(areaNameArgument);
        areaIsochroneCommand.AddOption(deleteAreaIsochroneOption);
        areaIsochroneCommand.AddOption(loggingOption);

        areaIsochroneCommand.SetHandler(async (string areaName, bool delete, string loggingLevel) =>
        {
            await InitializeLoggingAndConfigurationAsync(loggingLevel);
            if (delete)
            {
                await AreaManager.DeleteAreaIsochronesAsync(areaName, _logger, _configuration);
            }
            else
            {
                await AreaManager.RecreateAreaIsochronesAsync(areaName, _logger, _configuration);
            }
        }, areaNameArgument, deleteAreaIsochroneOption, loggingOption);

        // Add commands to area group
        areaCommand.AddCommand(createCommand);
        areaCommand.AddCommand(deleteCommand);
        areaCommand.AddCommand(listCommand);
        areaCommand.AddCommand(areaIsochroneCommand);

        // Add station command group
        var stationCommand = new Command("station", "Manage stations for areas");

        // Create station list command
        var stationListCommand = new Command("list", "List all stations for a specific area");
        var areaIdArgument = new Argument<string>("areaid", "The area ID to list stations for");
        var filterOption = new Option<string>(
            aliases: new[] { "--filter" },
            description: "Filter stations by rowkey or name containing the specified text")
        {
            ArgumentHelpName = "text"
        };

        stationListCommand.AddArgument(areaIdArgument);
        stationListCommand.AddOption(filterOption);
        stationListCommand.AddOption(loggingOption);

        stationListCommand.SetHandler(async (string areaId, string filter, string loggingLevel) =>
        {
            await InitializeLoggingAndConfigurationAsync(loggingLevel);
            await AreaManager.ListStationsAsync(areaId, filter, _logger, _configuration);
        }, areaIdArgument, filterOption, loggingOption);

        // Add commands to station group
        stationCommand.AddCommand(stationListCommand);

        // Create station isochrone command
        var stationIsochroneCommand = new Command("isochrone", "Generate isochrone data for a specific station");
        var areaIdIsochroneArgument = new Argument<string>("areaid", "The area ID containing the station");
        var stationIdArgument = new Argument<string>("stationid", "The station ID to generate isochrones for");
        var deleteOption = new Option<string?>(
            aliases: new[] { "--delete" },
            parseArgument: result =>
            {
                // If --delete is specified without a value, return empty string (delete all)
                // If --delete is specified with a value, return that value
                // If --delete is not specified, return null
                if (result.Tokens.Count == 0)
                {
                    return "";  // Delete all
                }
                return result.Tokens[0].Value;
            },
            description: "Delete isochrone(s). Use without value to delete all, or specify duration (5, 10, 15, 20, 30) to delete specific isochrone")
        {
            ArgumentHelpName = "duration",
            Arity = ArgumentArity.ZeroOrOne
        };

        stationIsochroneCommand.AddArgument(areaIdIsochroneArgument);
        stationIsochroneCommand.AddArgument(stationIdArgument);
        stationIsochroneCommand.AddOption(deleteOption);
        stationIsochroneCommand.AddOption(loggingOption);

        stationIsochroneCommand.SetHandler(async (string areaId, string stationId, string? deleteValue, string loggingLevel) =>
        {
            await InitializeLoggingAndConfigurationAsync(loggingLevel);
            
            // Parse delete option
            int? deleteDuration = null;
            bool isDeleteMode = false;
            
            if (deleteValue != null)
            {
                isDeleteMode = true;
                if (!string.IsNullOrEmpty(deleteValue))
                {
                    if (int.TryParse(deleteValue, out var parsedDuration))
                    {
                        deleteDuration = parsedDuration;
                    }
                    else
                    {
                        Console.WriteLine($"❌ Invalid duration value '{deleteValue}'. Valid values are: 5, 10, 15, 20, 30");
                        Environment.Exit(1);
                        return;
                    }
                }
                else
                {
                    deleteDuration = 0; // 0 means delete all
                }
            }
            
            await AreaManager.GenerateStationIsochroneAsync(areaId, stationId, isDeleteMode, deleteDuration, _logger, _configuration);
        }, areaIdIsochroneArgument, stationIdArgument, deleteOption, loggingOption);

        stationCommand.AddCommand(stationIsochroneCommand);

        // Add commands to root
        rootCommand.AddCommand(areaCommand);
        rootCommand.AddCommand(stationCommand);

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
