using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.App.Services;
using XivLinker.App.ViewModels;
using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models;
using XivLinker.Infrastructure.Overlay.Models;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.Tests;

public sealed class AutoCraftViewModelTests
{
    [Fact]
    public void Constructor_AutoDetectsCurrentCrafterJob()
    {
        var currentPlayerStateService = new FakeCurrentPlayerStateService(new CurrentPlayerState
        {
            ClassJobId = 8,
        });

        var viewModel = CreateViewModel(currentPlayerStateService);

        Assert.True(viewModel.IsCrafterJobAutoDetected);
        Assert.False(viewModel.CanChangeCrafterJob);
        Assert.Equal(CrafterJobDetectionState.Crafter, viewModel.DetectionState);
        Assert.Equal(8u, viewModel.SelectedCrafterJob?.Job?.ClassJobId);
    }

    [Fact]
    public void Constructor_ShowsNonCrafterState_WhenCurrentJobIsNotCrafter()
    {
        var currentPlayerStateService = new FakeCurrentPlayerStateService(new CurrentPlayerState
        {
            ClassJobId = 1,
        });

        var viewModel = CreateViewModel(currentPlayerStateService);

        Assert.True(viewModel.IsCrafterJobAutoDetected);
        Assert.True(viewModel.IsCurrentJobNonCrafter);
        Assert.False(viewModel.CanChangeCrafterJob);
        Assert.Equal(CrafterJobDetectionState.NonCrafter, viewModel.DetectionState);
        Assert.Null(viewModel.SelectedCrafterJob?.Job);
        Assert.Equal("クラフター以外", viewModel.SelectedCrafterJob?.DisplayName);
        Assert.Single(viewModel.DisplayedCrafterJobs);
    }

    [Fact]
    public void Constructor_AllowsManualSelection_WhenCurrentJobIsUnknown()
    {
        var currentPlayerStateService = new FakeCurrentPlayerStateService(new CurrentPlayerState());

        var viewModel = CreateViewModel(currentPlayerStateService);

        Assert.False(viewModel.IsCrafterJobAutoDetected);
        Assert.False(viewModel.IsCurrentJobNonCrafter);
        Assert.True(viewModel.CanChangeCrafterJob);
        Assert.Equal(CrafterJobDetectionState.Unknown, viewModel.DetectionState);
        Assert.Equal(XivLinker.Domain.Models.CrafterJobs.All.Count, viewModel.DisplayedCrafterJobs.Count);
        Assert.NotNull(viewModel.SelectedCrafterJob);
    }

    private static AutoCraftViewModel CreateViewModel(IOverlayPluginCurrentPlayerStateService currentPlayerStateService)
    {
        return new AutoCraftViewModel(
            new FakeCraftSequenceStore(),
            new FakeAutoCraftSequenceEditorDialogService(),
            new OverlayWindowService(),
            new AutoCraftExecutionService(
                new OverlayWindowService(),
                new FakeAutoCraftActionExecutor(),
                new AppEventLogViewModel(),
                NullLogger<AutoCraftExecutionService>.Instance),
            new FakeCraftHotbarRegistrationValidator(),
            currentPlayerStateService);
    }

    private sealed class FakeCurrentPlayerStateService : IOverlayPluginCurrentPlayerStateService
    {
        public FakeCurrentPlayerStateService(CurrentPlayerState currentState)
        {
            CurrentState = currentState;
        }

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public CurrentPlayerState CurrentState { get; }
    }

    private sealed class FakeCraftSequenceStore : ICraftSequenceStore
    {
        public IReadOnlyList<CraftSequence> GetAll() => [];

        public CraftSequence? Find(Guid sequenceId) => null;

        public void Save(CraftSequence sequence)
        {
        }

        public void Delete(Guid sequenceId)
        {
        }
    }

    private sealed class FakeAutoCraftSequenceEditorDialogService : IAutoCraftSequenceEditorDialogService
    {
        public Task ShowEditorAsync(CraftSequence? sequence, Action<CraftSequence> save) => Task.CompletedTask;
    }

    private sealed class FakeAutoCraftActionExecutor : IAutoCraftActionExecutor
    {
        public Task ExecuteAsync(
            CraftSequence sequence,
            int runCount,
            Action<string>? reportStatus,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCraftHotbarRegistrationValidator : ICraftHotbarRegistrationValidator
    {
        public Task<XivLinker.Application.Models.CraftSequenceValidationResult> ValidateAsync(
            CraftSequence sequence,
            CrafterJob crafterJob,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new XivLinker.Application.Models.CraftSequenceValidationResult());
        }
    }
}
