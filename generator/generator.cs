
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

    static bool BuildStations()
    {
        string mapKey = config["subscriptionKey"]!.ToString();
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

                //latitude and longitude ensure that use . for decimal point and not
                string centerString = $"{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}";

                JObject metroStationData = new JObject();
                for (int d = 0; d < distances.Count; d++)
                {
                    double distance = (double)distances[d]!;

                    Console.WriteLine($"generating area for {name} with distance {distance}");

                    // build url
                    string url = $"https://atlas.microsoft.com/route/range/json?api-version=1.0&query={centerString}&distanceBudgetInMeters={distance}&TravelMode=car&avoid=motorways&traffic=false&vehicleMaxSpeed=5&subscription-key={mapKey}";
                    //Console.WriteLine($"calling {url}");

                    // call api
                    string response = new HttpClient().GetStringAsync(url).Result;

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

                    metroStationData.Add($"distance{distance}", convertedPoints); 
                }

                // save response to file
                string filename = $"{stations}/{name}-{id}.json";
                File.WriteAllText(filename, metroStationData.ToString());
            }
        }
        return true;
    }
}