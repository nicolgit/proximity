
using System.Dynamic;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

internal class Program
{
    private static JObject config = JObject.Parse(File.ReadAllText("generator.config.json"));

    private static void Main(string[] args)
    {
        Console.WriteLine("Hello!");

        if (config["buildStations"]!.Value<bool>() == true)
        {
            BuildStations();
        }

    }

    enum API
    {
        none,
        Azure,
        MapBox
    }

    static string azureMapKey = config["azureSubscriptionKey"]!.ToString();
    static string mapBoxKey = config["mapBoxSubscriptionKey"]!.ToString();
    static string mapApiString = config["API"]!.ToString();

    static bool BuildStations()
    {
        API mapApi = API.none;
        if (mapApiString == "Azure")
        {
            mapApi = API.Azure;
        }
        else if (mapApiString == "MapBox")
        {
            mapApi = API.MapBox;
        }
        else
        {
            throw new Exception($"Invalid API selected: ${mapApiString}");
        }

        JArray distances = (JArray)config["distances"]!;
        string stations = config["stations"]!.ToString();

        foreach (string metro in config["metro"]!)
        {
            Console.WriteLine($"generating stations data for metro file {metro}");
            string jsonMetro = File.ReadAllText(metro!);

            JObject metroData = JObject.Parse(jsonMetro);
            JArray stops = (JArray)metroData["stops"]!;
            for (int id = 0; id < stops.Count; id++)
            {
                var item = stops[id];
                string name = item[0]!.ToString();
                double latitude = (double)item[1]!;
                double longitude = (double)item[2]!;

                JObject metroStationData = new JObject();
                for (int d = 0; d < distances.Count; d++)
                {
                    double distance = (double)distances[d]!;

                    Console.WriteLine($"generating area for {name} with distance {distance}");

                    JArray rangePolygon;

                    if (mapApi == API.Azure) rangePolygon = callAzureApi(longitude, latitude, distance);
                    else rangePolygon = callMapBoxApi(longitude, latitude, distance);

                    metroStationData.Add($"distance{distance}", rangePolygon);
                }

                // save response to file
                string filename = $"{stations}/{name}-{id}.json";
                File.WriteAllText(filename, metroStationData.ToString());
            }
        }
        return true;
    }

    static JArray callAzureApi(double longitude, double latitude, double distance)
    {
        //latitude and longitude ensure that use . for decimal point and not
        string centerString = $"{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}";

        // build url
        string url = $"https://atlas.microsoft.com/route/range/json?api-version=1.0&query={centerString}&distanceBudgetInMeters={distance}&TravelMode=car&avoid=motorways&traffic=false&vehicleMaxSpeed=5&subscription-key={azureMapKey}";
        Console.WriteLine($"calling {url}");

        string response = "";

        // call api
        try
        {
            response = new HttpClient().GetStringAsync(url).Result;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error calling Azure API: {e.Message}");
            throw;
        }

        JObject jsonResp = JObject.Parse(response);

        // get array at root > reachableRange > boundary
        JArray points = (JArray)jsonResp.Root["reachableRange"]!["boundary"]!;

        //Convert from array of {"latitude":41.90627,"longitude":12.41428} to array of  [41.90627,12.41428]
        JArray convertedPoints = new JArray();
        foreach (JObject point in points)
        {
            double lat = (double)point["latitude"]!;
            double lon = (double)point["longitude"]!;
            convertedPoints.Add(new JArray(lat, lon));
        }
        return convertedPoints;
    }

    static JArray callMapBoxApi(double longitude, double latitude, double distance) {
        //latitude and longitude ensure that use . for decimal point and not
        string centerString = $"{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}";

        // build url
        string url = $"https://api.mapbox.com/isochrone/v1/mapbox/walking/{centerString}?contours_meters={distance}&polygons=true&denoise=1&access_token={mapBoxKey}";
        
        Console.WriteLine($"calling {url}");

        string response = "";

        // call api
        try
        {
            response = new HttpClient().GetStringAsync(url).Result;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error calling MapBox API: {e.Message}");
            throw;
        }

        JObject jsonResp = JObject.Parse(response);

        // get array at root > features > geometry > coordinates
        JArray points = (JArray)jsonResp["features"]![0]!["geometry"]!["coordinates"]![0]!;

        //Convert from array of [12.41428,41.90627] to array of  [41.90627,12.41428]
        JArray convertedPoints = new JArray();
        foreach (JArray point in points)
        {
            double lat = (double)point[1]!;
            double lon = (double)point[0]!;
            convertedPoints.Add(new JArray(lat, lon));
        }
        return convertedPoints;
    }
}