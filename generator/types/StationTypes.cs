namespace Generator.Types;

public enum StationTypes
{
    Station,
    TramStop,
    TrolleybusStop,
    BusStop,
    MetroStation,
    Undefined
}

public static class StationTypeExtensions
{
    public static string ToStringValue(this StationTypes stationType)
    {
        return stationType switch
        {
            StationTypes.Station => "station",
            StationTypes.TramStop => "tram_stop",
            StationTypes.TrolleybusStop => "trolleybus",
            StationTypes.BusStop => "bus_stop",
            StationTypes.MetroStation => "metro_station",
            StationTypes.Undefined => "undefined",
            _ => "unknown"
        };
    }

    public static StationTypes FromString(string value)
    {
        return value switch
        {
            "station" => StationTypes.Station,
            "tram_stop" => StationTypes.TramStop,
            "trolleybus" => StationTypes.TrolleybusStop,
            "bus_stop" => StationTypes.BusStop,
            "metro_station" => StationTypes.MetroStation,
            "undefined" => StationTypes.Undefined,
            _ => throw new ArgumentException($"Unknown station type: {value}")
        };
    }
}