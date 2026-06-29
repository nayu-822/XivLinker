using XivLinker.App.ViewModels;

namespace XivLinker.Tests;

public sealed class AppEventLogViewModelTests
{
    [Fact]
    public void Add_SkipsImmediateDuplicateMessages()
    {
        AppEventLogViewModel viewModel = new();
        int initialCount = viewModel.Items.Count;

        viewModel.Add("duplicate message", "Info");
        viewModel.Add("duplicate message", "Info");

        Assert.Equal(initialCount + 1, viewModel.Items.Count);
    }
}
