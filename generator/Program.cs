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
        var loggingOption = new Option<string>("--logging", "-l")
        {
            Description = "Set the minimum log level (Trace, Debug, Information, Warning, Error, Critical, None)",
            HelpName = "level",
            DefaultValueFactory = _ => "None"
        };

        // Add area command group
        var areaCommand = new Command("area", "Manage areas for proximity calculations");

        // Create area create command
        var createCommand = new Command("create", "Create a new area");

        var nameArgument = new Argument<string>("name")
        {
            Description = "The name of the area"
        };
        var centerOption = new Option<string>("--center")
        {
            Description = "Center coordinates as 'latitude,longitude'",
            Required = true,
            HelpName = "lat,lon"
        };
        var diameterOption = new Option<int>("--diameter")
        {
            Description = "Diameter in meters",
            Required = true,
            HelpName = "meters"
        };
        var displayNameOption = new Option<string>("--displayname")
        {
            Description = "Display name for the area",
            Required = true,
            HelpName = "display_name"
        };
        var developerOption = new Option<bool>("--developer")
        {
            Description = "Developer mode: limit to first 3 stations and 3 tram stops",
            HelpName = "developer"
        };
        var noIsochroneOption = new Option<bool>("--noisochrone")
        {
            Description = "Skip isochrone generation when creating the area",
            HelpName = "noisochrone"
        };

        createCommand.Add(nameArgument);
        createCommand.Add(centerOption);
        createCommand.Add(diameterOption);
        createCommand.Add(displayNameOption);
        createCommand.Add(developerOption);
        createCommand.Add(noIsochroneOption);
        createCommand.Add(loggingOption);

        createCommand.SetAction(async parseResult =>
        {
            await InitializeLoggingAndConfigurationAsync(parseResult.GetValue(loggingOption)!);
            await AreaManager.CreateAreaAsync(
                parseResult.GetValue(nameArgument)!,
                parseResult.GetValue(centerOption)!,
                parseResult.GetValue(diameterOption),
                parseResult.GetValue(displayNameOption)!,
                parseResult.GetValue(developerOption),
                parseResult.GetValue(noIsochroneOption),
                _logger,
                _configuration);
        });

        // Create area delete command
        var deleteCommand = new Command("delete", "Delete an area");
        var deleteNameArgument = new Argument<string>("name")
        {
            Description = "The name of the area to delete"
        };

        deleteCommand.Add(deleteNameArgument);
        deleteCommand.Add(loggingOption);

        deleteCommand.SetAction(async parseResult =>
        {
            await InitializeLoggingAndConfigurationAsync(parseResult.GetValue(loggingOption)!);
            await AreaManager.DeleteAreaAsync(parseResult.GetValue(deleteNameArgument)!, _logger, _configuration);
        });

        // Create area list command
        var listCommand = new Command("list", "List all areas");

        listCommand.Add(loggingOption);

        listCommand.SetAction(async parseResult =>
        {
            await InitializeLoggingAndConfigurationAsync(parseResult.GetValue(loggingOption)!);
            await AreaManager.ListAreasAsync(_logger, _configuration);
        });

        // Create area isochrone command
        var areaIsochroneCommand = new Command("isochrone", "Recreate or delete area-wide isochrones using existing station isochrones");
        var areaNameArgument = new Argument<string>("areaname")
        {
            Description = "The name of the area to regenerate isochrones for"
        };
        var deleteAreaIsochroneOption = new Option<bool>("--delete")
        {
            Description = "Delete area-wide isochrones instead of creating them",
            HelpName = "delete"
        };

        areaIsochroneCommand.Add(areaNameArgument);
        areaIsochroneCommand.Add(deleteAreaIsochroneOption);
        areaIsochroneCommand.Add(loggingOption);

        areaIsochroneCommand.SetAction(async parseResult =>
        {
            await InitializeLoggingAndConfigurationAsync(parseResult.GetValue(loggingOption)!);
            var areaName = parseResult.GetValue(areaNameArgument)!;
            if (parseResult.GetValue(deleteAreaIsochroneOption))
            {
                await AreaManager.DeleteAreaIsochronesAsync(areaName, _logger, _configuration);
            }
            else
            {
                await AreaManager.RecreateAreaIsochronesAsync(areaName, _logger, _configuration);
            }
        });

        // Add commands to area group
        areaCommand.Add(createCommand);
        areaCommand.Add(deleteCommand);
        areaCommand.Add(listCommand);
        areaCommand.Add(areaIsochroneCommand);

        // Add station command group
        var stationCommand = new Command("station", "Manage stations for areas");

        // Create station list command
        var stationListCommand = new Command("list", "List all stations for a specific area");
        var areaIdArgument = new Argument<string>("areaid")
        {
            Description = "The area ID to list stations for"
        };
        var filterOption = new Option<string?>("--filter")
        {
            Description = "Filter stations by rowkey or name containing the specified text",
            HelpName = "text"
        };

        stationListCommand.Add(areaIdArgument);
        stationListCommand.Add(filterOption);
        stationListCommand.Add(loggingOption);

        stationListCommand.SetAction(async parseResult =>
        {
            await InitializeLoggingAndConfigurationAsync(parseResult.GetValue(loggingOption)!);
            await StationManager.ListStationsAsync(
                parseResult.GetValue(areaIdArgument)!,
                parseResult.GetValue(filterOption),
                _logger,
                _configuration);
        });

        // Add commands to station group
        stationCommand.Add(stationListCommand);

        // Add commands to root
        rootCommand.Add(areaCommand);
        rootCommand.Add(stationCommand);

        // Parse and invoke the command
        return await rootCommand.Parse(args).InvokeAsync();
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

        // Test required service connections
        var storageTestPassed = await TestManager.TestAzureStorageConnectionAsync(_logger, _configuration);
        var mapApiTestPassed = await TestManager.TestMapBoxApiKeyAsync(_logger, _configuration);
        var overpassApiTestPassed = await TestManager.TestOverpassApiAsync(_logger, _configuration);

        // Exit if any critical tests failed
        if (!storageTestPassed || !mapApiTestPassed || !overpassApiTestPassed)
        {
            _logger?.LogError("One or more critical service tests failed. Exiting application.");
            Console.WriteLine("❌ Critical service validation failed. Please check your configuration.");
            Environment.Exit(1);
        }

        // Add a small delay to make this truly async
        await Task.Delay(1);
    }
}
