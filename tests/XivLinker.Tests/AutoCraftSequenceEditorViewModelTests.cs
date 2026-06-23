using XivLinker.App.Services;
using XivLinker.App.ViewModels;
using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Tests;

public sealed class AutoCraftSequenceEditorViewModelTests
{
    [Fact]
    public async Task SaveCommand_WithNoSteps_DoesNotSave()
    {
        bool saved = false;
        AutoCraftSequenceEditorViewModel viewModel = CreateViewModel(_ => saved = true);

        await viewModel.LoadAsync(null);
        viewModel.CurrentSteps.Clear();

        viewModel.SaveCommand.Execute(null);

        Assert.False(saved);
        Assert.Equal("シーケンスにアクションを1件以上追加してください。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task AddAction_AddsPaletteActionToSequence()
    {
        AutoCraftSequenceEditorViewModel viewModel = CreateViewModel(_ => { });

        await viewModel.LoadAsync(null);
        viewModel.AddAction(CraftActionId.BasicTouch);

        CraftSequenceStepViewModel step = Assert.Single(viewModel.CurrentSteps);
        Assert.Equal(CraftActionId.BasicTouch, step.ActionId);
        Assert.Equal("加工", step.DisplayName);
    }

    [Fact]
    public async Task SaveCommand_WithInvalidWaitMilliseconds_DoesNotSave()
    {
        bool saved = false;
        AutoCraftSequenceEditorViewModel viewModel = CreateViewModel(_ => saved = true);

        await viewModel.LoadAsync(null);
        viewModel.AddAction(CraftActionId.BasicSynthesis);
        CraftSequenceStepViewModel step = Assert.Single(viewModel.CurrentSteps);
        step.WaitMilliseconds = 0;

        viewModel.SaveCommand.Execute(null);

        Assert.False(saved);
        Assert.Equal("待機時間は1ms以上で入力してください。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SaveCommand_SavesActionIdBasedSequence()
    {
        CraftSequence? saved = null;
        AutoCraftSequenceEditorViewModel viewModel = CreateViewModel(sequence => saved = sequence);

        await viewModel.LoadAsync(null);
        viewModel.AddAction(CraftActionId.ByregotsBlessing);
        CraftSequenceStepViewModel step = Assert.Single(viewModel.CurrentSteps);
        step.WaitMilliseconds = 3200;

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(saved);
        CraftSequenceStep savedStep = Assert.Single(saved!.Steps);
        Assert.Equal(CraftActionId.ByregotsBlessing, savedStep.ActionId);
        Assert.Equal(3200, savedStep.WaitMilliseconds);
    }

    [Fact]
    public async Task LoadAsync_WhenCatalogFails_UsesFallbackActionsAndStillLoadsExistingSequence()
    {
        var failingService = new FakeCrafterActionCatalogService(
            new CrafterActionCatalogResult(CraftActionCatalog.GetAll(), "Lumina initialization failed."));
        var viewModel = new AutoCraftSequenceEditorViewModel(
            failingService,
            new CraftActionIconSourceService(failingService),
            () => { },
            _ => { });
        var sequence = new CraftSequence
        {
            SequenceId = Guid.NewGuid(),
            Name = "既存シーケンス",
            Steps =
            [
                new CraftSequenceStep
                {
                    ActionId = CraftActionId.BasicSynthesis,
                    WaitMilliseconds = 2500,
                },
            ],
        };

        await viewModel.LoadAsync(sequence);

        Assert.Equal("Lumina initialization failed.", viewModel.LoadErrorMessage);
        Assert.NotEmpty(viewModel.AvailableActions);
        Assert.Single(viewModel.CurrentSteps);
        Assert.Equal("作業", viewModel.CurrentSteps[0].DisplayName);
    }

    private static AutoCraftSequenceEditorViewModel CreateViewModel(Action<CraftSequence> save)
    {
        var service = new FakeCrafterActionCatalogService(
            new CrafterActionCatalogResult(
            [
                new CraftActionDefinition(CraftActionId.BasicSynthesis, "作業", 2500, "クラフターアクション", 0, []),
                new CraftActionDefinition(CraftActionId.BasicTouch, "加工", 2500, "クラフターアクション", 0, []),
                new CraftActionDefinition(CraftActionId.ByregotsBlessing, "ビエルゴの祝福", 2500, "クラフターアクション", 0, []),
            ]));

        return new AutoCraftSequenceEditorViewModel(
            service,
            new CraftActionIconSourceService(service),
            () => { },
            save);
    }

    private sealed class FakeCrafterActionCatalogService : ICrafterActionCatalogService
    {
        private readonly CrafterActionCatalogResult result;

        public FakeCrafterActionCatalogService(CrafterActionCatalogResult result)
        {
            this.result = result;
        }

        public Task<CrafterActionCatalogResult> GetCrafterActionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }

        public Task<byte[]?> GetIconPngAsync(uint iconId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<byte[]?>(null);
        }
    }
}
