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
        var stationIsochroneCommand = new Command("isochrone", "Generate isochrone data for a specific station or all stations in an area");
        
        // When called with <areaid> <stationid>, it processes a specific station
        var areaIdIsochroneArgument = new Argument<string>("areaid", "The area ID containing the station");
        var stationIdArgument = new Argument<string?>("stationid", "The station ID to generate isochrones for (optional - if not provided, processes all stations in the area)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };
        
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
            description: "Delete isochrone(s). When used with area only: deletes all station isochrones in the area. When used with area and station: deletes isochrones for that specific station. Use without value to delete all durations, or specify duration (5, 10, 15, 20, 30) to delete specific isochrone")
        {
            ArgumentHelpName = "duration",
            Arity = ArgumentArity.ZeroOrOne
        };

        stationIsochroneCommand.AddArgument(areaIdIsochroneArgument);
        stationIsochroneCommand.AddArgument(stationIdArgument);
        stationIsochroneCommand.AddOption(deleteOption);
        stationIsochroneCommand.AddOption(loggingOption);

        stationIsochroneCommand.SetHandler(async (string areaId, string? stationId, string? deleteValue, string loggingLevel) =>
        {
            await InitializeLoggingAndConfigurationAsync(loggingLevel);
            
            if (string.IsNullOrWhiteSpace(stationId))
            {
                // No station ID provided
                if (deleteValue != null)
                {
                    // Delete mode - delete all station isochrones in the area
                    await AreaManager.DeleteAllStationIsochronesAsync(areaId, _logger, _configuration);
                }
                else
                {
                    // Regenerate mode - regenerate all stations in the area
                    await AreaManager.RegenerateAllStationIsochronesAsync(areaId, _logger, _configuration);
                }
            }
            else
            {
                // Station ID provided - process specific station
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
            }
        }, areaIdIsochroneArgument, stationIdArgument, deleteOption, loggingOption);

        stationCommand.AddCommand(stationIsochroneCommand);

        // Create station regenerate command for area-wide regeneration
        var stationRegenerateCommand = new Command("regenerate", "Regenerate isochrones for all stations in an area");
        var areaIdRegenerateArgument = new Argument<string>("areaid", "The area ID to regenerate all station isochrones for");

        stationRegenerateCommand.AddArgument(areaIdRegenerateArgument);
        stationRegenerateCommand.AddOption(loggingOption);

        stationRegenerateCommand.SetHandler(async (string areaId, string loggingLevel) =>
        {
            await InitializeLoggingAndConfigurationAsync(loggingLevel);
            await AreaManager.RegenerateAllStationIsochronesAsync(areaId, _logger, _configuration);
        }, areaIdRegenerateArgument, loggingOption);

        stationCommand.AddCommand(stationRegenerateCommand);

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
        var storageTestPassed = await AreaManager.TestAzureStorageConnectionAsync(_logger, _configuration);
        var mapApiTestPassed = await AreaManager.TestMapBoxApiKeyAsync(_logger, _configuration);

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
}
