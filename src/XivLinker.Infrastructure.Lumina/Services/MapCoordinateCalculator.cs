namespace XivLinker.Infrastructure.Lumina.Services;

public static class MapCoordinateCalculator
{
    public static double ConvertWorldToMapCoordinate(float worldCoordinate, short offset, ushort sizeFactor)
    {
        double scale = sizeFactor / 100.0;
        double value = (41.0 / scale) * ((worldCoordinate + offset) / 2048.0) + 1.0;
        return Math.Round(value, 1, MidpointRounding.AwayFromZero);
    }

    public static string FormatCoordinates(double x, double y)
    {
        return $"X: {x:0.0} / Y: {y:0.0}";
    }
}
