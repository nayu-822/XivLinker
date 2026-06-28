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
    public void ConvertWorldToMapCoordinate_MatchesReferenceFormula()
    {
        double position = 100d;
        double offset = 0d;
        double sizeFactor = 100d;
        double scale = sizeFactor / 100d;
        double expected = Math.Truncate((((((position + offset) * scale) + 1024d) / 2048d) * 41d / scale + 1d) * 10d) / 10d;

        double actual = MapCoordinateCalculator.ConvertWorldToMapCoordinate(position, offset, sizeFactor);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FormatCoordinates_FormatsWithSingleDecimal()
    {
        string actual = MapCoordinateCalculator.FormatCoordinates(12.34, 8.76);

        Assert.Equal("X: 12.3 / Y: 8.8", actual);
    }

    [Fact]
    public void FormatCoordinates_ReturnsFallbackForInvalidValues()
    {
        string actual = MapCoordinateCalculator.FormatCoordinates(double.NaN, 8.76);

        Assert.Equal("座標を変換できません", actual);
    }
}
