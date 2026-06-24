using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Tests;

public sealed class CraftActionCatalogTests
{
    [Theory]
    [InlineData("craftaction:basic-synthesis", 3000)]
    [InlineData("craftaction:basic-touch", 3000)]
    [InlineData("craftaction:observe", 2000)]
    [InlineData("craftaction:veneration", 2000)]
    [InlineData("craftaction:great-strides", 2000)]
    [InlineData("craftaction:manipulation", 2000)]
    [InlineData("craftaction:final-appraisal", 2000)]
    [InlineData("craftaction:unknown-action", 3000)]
    public void ResolvePostActionWaitMilliseconds_ReturnsExpectedValue(string actionIdValue, int expected)
    {
        int actual = CraftActionCatalog.ResolvePostActionWaitMilliseconds(new CraftActionId(actionIdValue));

        Assert.Equal(expected, actual);
    }
}
