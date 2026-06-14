using XivLinker.Domain.Models;

namespace XivLinker.Tests;

public sealed class StartupSmokeTests
{
    [Fact]
    public void DomainPlaceholder_CanBeConstructed()
    {
        var placeholder = new DomainModelPlaceholder();

        Assert.NotNull(placeholder);
    }
}
