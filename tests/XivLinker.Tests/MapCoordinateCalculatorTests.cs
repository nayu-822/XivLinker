using XivLinker.Infrastructure.Lumina.Services;

namespace XivLinker.Tests;

public sealed class MapCoordinateCalculatorTests
{
    [Theory]
    [InlineData(0f, 0, 100, 1.0)]
    [InlineData(1024f, 0, 100, 21.5)]
    [InlineData(512f, 100, 200, 7.1)]
    public void ConvertWorldToMapCoordinate_ReturnsExpectedValue(
        float worldCoordinate,
        short offset,
        ushort sizeFactor,
        double expected)
    {
        double actual = MapCoordinateCalculator.ConvertWorldToMapCoordinate(worldCoordinate, offset, sizeFactor);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FormatCoordinates_FormatsWithSingleDecimal()
    {
        string actual = MapCoordinateCalculator.FormatCoordinates(12.34, 8.76);

        Assert.Equal("X: 12.3 / Y: 8.8", actual);
    }
}
