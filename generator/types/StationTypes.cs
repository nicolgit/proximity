namespace Generator.Types;

public enum StationTypes
{
    Station,
    TramStop,
    TrolleybusStop,
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
            "undefined" => StationTypes.Undefined,
            _ => throw new ArgumentException($"Unknown station type: {value}")
        };
    }
}