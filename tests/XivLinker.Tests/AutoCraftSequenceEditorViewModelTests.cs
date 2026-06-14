using XivLinker.App.ViewModels;
using XivLinker.Domain.Models;

namespace XivLinker.Tests;

public sealed class AutoCraftSequenceEditorViewModelTests
{
    [Fact]
    public void SaveCommand_WithNoSteps_DoesNotSave()
    {
        bool saved = false;
        var viewModel = CreateViewModel(_ => saved = true);

        viewModel.Load(null);
        viewModel.Steps.Clear();

        viewModel.SaveCommand.Execute(null);

        Assert.False(saved);
        Assert.Equal("ステップを1件以上追加してください。", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveCommand_WithEmptyActionName_DoesNotSave()
    {
        bool saved = false;
        var viewModel = CreateViewModel(_ => saved = true);

        viewModel.Load(null);
        CraftSequenceStepViewModel step = Assert.Single(viewModel.Steps);
        step.ActionName = " ";

        viewModel.SaveCommand.Execute(null);

        Assert.False(saved);
        Assert.Equal("アクション名が空のステップがあります。", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveCommand_WithInvalidWaitMilliseconds_DoesNotSave()
    {
        bool saved = false;
        var viewModel = CreateViewModel(_ => saved = true);

        viewModel.Load(null);
        CraftSequenceStepViewModel step = Assert.Single(viewModel.Steps);
        step.WaitMilliseconds = 0;

        viewModel.SaveCommand.Execute(null);

        Assert.False(saved);
        Assert.Equal("待機時間は1以上で入力してください。", viewModel.StatusMessage);
    }

    private static AutoCraftSequenceEditorViewModel CreateViewModel(Action<CraftSequence> save)
    {
        return new AutoCraftSequenceEditorViewModel(
            () => { },
            save);
    }
}
