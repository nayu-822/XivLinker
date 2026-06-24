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
        Assert.Equal("シーケンスにアクションを1つ以上追加してください。", viewModel.StatusMessage);
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
    public async Task LoadAsync_StartsWithoutUnsavedChanges()
    {
        AutoCraftSequenceEditorViewModel viewModel = CreateViewModel(_ => { });

        await viewModel.LoadAsync(null);

        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task AddAction_MarksViewModelAsDirty()
    {
        AutoCraftSequenceEditorViewModel viewModel = CreateViewModel(_ => { });

        await viewModel.LoadAsync(null);
        viewModel.AddAction(CraftActionId.BasicSynthesis);

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task SaveCommand_SavesActionIdBasedSequence()
    {
        CraftSequence? saved = null;
        AutoCraftSequenceEditorViewModel viewModel = CreateViewModel(sequence => saved = sequence);

        await viewModel.LoadAsync(null);
        viewModel.AddAction(CraftActionId.ByregotsBlessing);

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(saved);
        CraftSequenceStep savedStep = Assert.Single(saved!.Steps);
        Assert.Equal(CraftActionId.ByregotsBlessing, savedStep.ActionId);
    }

    [Fact]
    public async Task LoadAsync_BuildsPaletteWithReferenceCategoriesAndOrder()
    {
        AutoCraftSequenceEditorViewModel viewModel = CreateViewModel(_ => { });

        await viewModel.LoadAsync(null);

        Assert.Collection(
            viewModel.AvailableActions,
            synthesis =>
            {
                Assert.Equal("作業系", synthesis.CategoryName);
                Assert.Equal(
                    [
                        CraftActionId.BasicSynthesis,
                        CraftActionId.CarefulSynthesis,
                        CraftActionId.IntensiveSynthesis,
                        CraftActionId.DelicateSynthesis,
                        CraftActionId.RapidSynthesis,
                        CraftActionId.MuscleMemory,
                        CraftActionId.Groundwork,
                        CraftActionId.PrudentSynthesis,
                    ],
                    synthesis.Actions.Select(static action => action.ActionId).ToArray());
            },
            touch =>
            {
                Assert.Equal("加工系", touch.CategoryName);
                Assert.Equal(
                    [
                        CraftActionId.BasicTouch,
                        CraftActionId.StandardTouch,
                        CraftActionId.PreciseTouch,
                        CraftActionId.PrudentTouch,
                        CraftActionId.PreparatoryTouch,
                        CraftActionId.ByregotsBlessing,
                        CraftActionId.HastyTouch,
                        CraftActionId.AdvancedTouch,
                        CraftActionId.TrainedFinesse,
                        CraftActionId.RefinedTouch,
                        CraftActionId.DaringTouch,
                        CraftActionId.Reflect,
                    ],
                    touch.Actions.Select(static action => action.ActionId).ToArray());
            },
            buff =>
            {
                Assert.Equal("バフ・補助系", buff.CategoryName);
                Assert.Equal(
                    [
                        CraftActionId.MastersMend,
                        CraftActionId.ImmaculateMend,
                        CraftActionId.Manipulation,
                        CraftActionId.Veneration,
                        CraftActionId.Observe,
                        CraftActionId.TrainedEye,
                        CraftActionId.TricksOfTheTrade,
                        CraftActionId.WasteNot,
                        CraftActionId.WasteNotII,
                        CraftActionId.GreatStrides,
                        CraftActionId.Innovation,
                        CraftActionId.FinalAppraisal,
                        CraftActionId.TrainedPerfection,
                    ],
                    buff.Actions.Select(static action => action.ActionId).ToArray());
            },
            specialist =>
            {
                Assert.Equal("専門技能系", specialist.CategoryName);
                Assert.Equal(
                    [
                        CraftActionId.CarefulObservation,
                        CraftActionId.HeartAndSoul,
                        CraftActionId.QuickInnovation,
                    ],
                    specialist.Actions.Select(static action => action.ActionId).ToArray());
            });
    }

    [Fact]
    public async Task LoadAsync_DoesNotShowHiddenActionsInPalette()
    {
        AutoCraftSequenceEditorViewModel viewModel = CreateViewModel(_ => { });

        await viewModel.LoadAsync(null);

        CraftActionId[] paletteActionIds = viewModel.AvailableActions
            .SelectMany(static category => category.Actions)
            .Select(static action => action.ActionId)
            .ToArray();

        Assert.DoesNotContain(CraftActionId.FocusedSynthesis, paletteActionIds);
        Assert.DoesNotContain(CraftActionId.FocusedTouch, paletteActionIds);
    }

    [Fact]
    public async Task LoadAsync_WhenCatalogFails_UsesFallbackActionsAndStillLoadsHiddenSequenceAction()
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
                    ActionId = CraftActionId.FocusedSynthesis,
                },
            ],
        };

        await viewModel.LoadAsync(sequence);

        Assert.Equal("Lumina initialization failed.", viewModel.LoadErrorMessage);
        Assert.Equal(4, viewModel.AvailableActions.Count);
        Assert.Single(viewModel.CurrentSteps);
        Assert.Equal("注視作業", viewModel.CurrentSteps[0].DisplayName);

        CraftActionId[] paletteActionIds = viewModel.AvailableActions
            .SelectMany(static category => category.Actions)
            .Select(static action => action.ActionId)
            .ToArray();
        Assert.DoesNotContain(CraftActionId.FocusedSynthesis, paletteActionIds);
    }

    private static AutoCraftSequenceEditorViewModel CreateViewModel(Action<CraftSequence> save)
    {
        var service = new FakeCrafterActionCatalogService(
            new CrafterActionCatalogResult(CraftActionCatalog.GetAll()));

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
