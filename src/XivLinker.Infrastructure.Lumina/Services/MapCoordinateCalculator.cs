namespace XivLinker.Infrastructure.Lumina.Services;

public static class MapCoordinateCalculator
{
    public const string CoordinateConversionFailedText = "座標を変換できません";

    public static double ConvertWorldToMapCoordinate(double position, double offset, double sizeFactor)
    {
        if (sizeFactor <= 0)
        {
            return double.NaN;
        }

        double scale = sizeFactor / 100d;
        double value = ((((position + offset) * scale) + 1024d) / 2048d) * 41d / scale + 1d;
        return Math.Truncate(value * 10d) / 10d;
    }

    public static string FormatCoordinates(double x, double y)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
        {
            return CoordinateConversionFailedText;
        }

        return $"X: {x:0.0} / Y: {y:0.0}";
    }
}
