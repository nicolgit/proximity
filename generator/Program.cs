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
            await StationManager.ListStationsAsync(areaId, filter, _logger, _configuration);
        }, areaIdArgument, filterOption, loggingOption);

        // Add commands to station group
        stationCommand.AddCommand(stationListCommand);

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
        var storageTestPassed = await TestManager.TestAzureStorageConnectionAsync(_logger, _configuration);
        var mapApiTestPassed = await TestManager.TestMapBoxApiKeyAsync(_logger, _configuration);

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
