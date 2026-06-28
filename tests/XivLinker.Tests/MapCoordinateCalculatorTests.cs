using XivLinker.Infrastructure.Lumina.Services;

namespace XivLinker.Tests;

public sealed class MapCoordinateCalculatorTests
{
    [Theory]
    [InlineData(0d, 0d, 100d, 21.5)]
    [InlineData(1024d, 0d, 100d, 42.0)]
    [InlineData(100d, 0d, 100d, 23.5)]
    public void ConvertWorldToMapCoordinate_ReturnsExpectedValue(
        double worldCoordinate,
        double offset,
        double sizeFactor,
        double expected)
    {
        double actual = MapCoordinateCalculator.ConvertWorldToMapCoordinate(worldCoordinate, offset, sizeFactor);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConvertWorldToMapCoordinate_UsesReferenceFormula()
    {
        double position = 143.964;
        double offset = -657.0;
        double sizeFactor = 100.0;

        double scale = sizeFactor / 100d;
        double expected = Math.Truncate(
            (((((position + offset) * scale) + 1024d) / 2048d) * 41d / scale + 1d) * 10d) / 10d;

        double actual = MapCoordinateCalculator.ConvertWorldToMapCoordinate(
            position,
            offset,
            sizeFactor);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FormatCoordinates_UsesDashboardFormat()
    {
        string actual = MapCoordinateCalculator.FormatCoordinates(14.1, 14.1);

        Assert.Equal("X: 14.1 / Y: 14.1", actual);
    }

    [Fact]
    public void FormatCoordinates_ReturnsFallbackForInvalidValues()
    {
        string actual = MapCoordinateCalculator.FormatCoordinates(double.NaN, 8.76);

        Assert.Equal(MapCoordinateCalculator.CoordinateConversionFailedText, actual);
    }

    [Fact]
    public void CoordinateConversionFailedText_IsNotMojibake()
    {
        string text = MapCoordinateCalculator.CoordinateConversionFailedText;

        Assert.DoesNotContain("邵ｺ", text);
        Assert.DoesNotContain("郢ｧ", text);
        Assert.DoesNotContain("陟", text);
        Assert.DoesNotContain("隶", text);
    }
}
