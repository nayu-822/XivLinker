using XivLinker.App.ViewModels;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Tests;

public sealed class AutoCraftSequenceEditorViewModelTests
{
    [Fact]
    public void SaveCommand_WithNoSteps_DoesNotSave()
    {
        bool saved = false;
        var viewModel = CreateViewModel(_ => saved = true);

        viewModel.Load(null);
        viewModel.CurrentSteps.Clear();

        viewModel.SaveCommand.Execute(null);

        Assert.False(saved);
        Assert.Equal("シーケンスにアクションを1件以上追加してください。", viewModel.StatusMessage);
    }

    [Fact]
    public void AddAction_AddsPaletteActionToSequence()
    {
        var viewModel = CreateViewModel(_ => { });

        viewModel.Load(null);
        viewModel.AddAction(CraftActionId.BasicTouch);

        CraftSequenceStepViewModel step = Assert.Single(viewModel.CurrentSteps);
        Assert.Equal(CraftActionId.BasicTouch, step.ActionId);
        Assert.Equal("加工", step.DisplayName);
    }

    [Fact]
    public void SaveCommand_WithInvalidWaitMilliseconds_DoesNotSave()
    {
        bool saved = false;
        var viewModel = CreateViewModel(_ => saved = true);

        viewModel.Load(null);
        viewModel.AddAction(CraftActionId.BasicSynthesis);
        CraftSequenceStepViewModel step = Assert.Single(viewModel.CurrentSteps);
        step.WaitMilliseconds = 0;

        viewModel.SaveCommand.Execute(null);

        Assert.False(saved);
        Assert.Equal("待機時間は1ms以上で入力してください。", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveCommand_SavesActionIdBasedSequence()
    {
        CraftSequence? saved = null;
        var viewModel = CreateViewModel(sequence => saved = sequence);

        viewModel.Load(null);
        viewModel.AddAction(CraftActionId.ByregotsBlessing);
        CraftSequenceStepViewModel step = Assert.Single(viewModel.CurrentSteps);
        step.WaitMilliseconds = 3200;

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(saved);
        CraftSequenceStep savedStep = Assert.Single(saved!.Steps);
        Assert.Equal(CraftActionId.ByregotsBlessing, savedStep.ActionId);
        Assert.Equal(3200, savedStep.WaitMilliseconds);
    }

    private static AutoCraftSequenceEditorViewModel CreateViewModel(Action<CraftSequence> save)
    {
        return new AutoCraftSequenceEditorViewModel(
            () => { },
            save);
    }
}
