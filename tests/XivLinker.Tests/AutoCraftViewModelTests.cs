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

        AutoCraftViewModel viewModel = CreateViewModel(currentPlayerStateService);

        Assert.True(viewModel.IsCrafterJobAutoDetected);
        Assert.False(viewModel.CanChangeCrafterJob);
        Assert.Equal(CrafterJobDetectionState.Crafter, viewModel.DetectionState);
        Assert.Equal(8u, viewModel.SelectedCrafterJob?.Job?.ClassJobId);
    }

    [Fact]
    public void Constructor_ShowsDash_WhenCurrentJobIsNotCrafter()
    {
        var currentPlayerStateService = new FakeCurrentPlayerStateService(new CurrentPlayerState
        {
            ClassJobId = 1,
        });

        AutoCraftViewModel viewModel = CreateViewModel(currentPlayerStateService);

        Assert.True(viewModel.IsCrafterJobAutoDetected);
        Assert.True(viewModel.IsCurrentJobNonCrafter);
        Assert.False(viewModel.CanChangeCrafterJob);
        Assert.Equal(CrafterJobDetectionState.NonCrafter, viewModel.DetectionState);
        Assert.Null(viewModel.SelectedCrafterJob?.Job);
        Assert.Equal("-", viewModel.SelectedCrafterJob?.DisplayName);
        Assert.Single(viewModel.DisplayedCrafterJobs);
    }

    [Fact]
    public void Constructor_AllowsManualSelection_WhenCurrentJobIsUnknown()
    {
        var currentPlayerStateService = new FakeCurrentPlayerStateService(new CurrentPlayerState());

        AutoCraftViewModel viewModel = CreateViewModel(currentPlayerStateService);

        Assert.False(viewModel.IsCrafterJobAutoDetected);
        Assert.False(viewModel.IsCurrentJobNonCrafter);
        Assert.True(viewModel.CanChangeCrafterJob);
        Assert.Equal(CrafterJobDetectionState.Unknown, viewModel.DetectionState);
        Assert.Equal(XivLinker.Domain.Models.CrafterJobs.All.Count, viewModel.DisplayedCrafterJobs.Count);
        Assert.NotNull(viewModel.SelectedCrafterJob);
    }

    [Fact]
    public void StateChanged_UpdatesSelectedCrafterJob_WhenCrafterJobChanges()
    {
        var currentPlayerStateService = new MutableCurrentPlayerStateService(new CurrentPlayerState
        {
            ClassJobId = 8,
        });

        AutoCraftViewModel viewModel = CreateViewModel(currentPlayerStateService);

        currentPlayerStateService.SetClassJobId(9);

        Assert.Equal(CrafterJobDetectionState.Crafter, viewModel.DetectionState);
        Assert.Equal(9u, viewModel.SelectedCrafterJob?.Job?.ClassJobId);
        Assert.False(viewModel.CanChangeCrafterJob);
    }

    [Fact]
    public void StateChanged_UpdatesToNonCrafterState_WhenLeavingCrafterJob()
    {
        var currentPlayerStateService = new MutableCurrentPlayerStateService(new CurrentPlayerState
        {
            ClassJobId = 8,
        });

        AutoCraftViewModel viewModel = CreateViewModel(currentPlayerStateService);

        currentPlayerStateService.SetClassJobId(1);

        Assert.Equal(CrafterJobDetectionState.NonCrafter, viewModel.DetectionState);
        Assert.Equal("-", viewModel.SelectedCrafterJob?.DisplayName);
        Assert.False(viewModel.CanChangeCrafterJob);
        Assert.Single(viewModel.DisplayedCrafterJobs);
    }

    [Fact]
    public void StateChanged_UpdatesToCrafterState_WhenReturningFromNonCrafter()
    {
        var currentPlayerStateService = new MutableCurrentPlayerStateService(new CurrentPlayerState
        {
            ClassJobId = 1,
        });

        AutoCraftViewModel viewModel = CreateViewModel(currentPlayerStateService);

        currentPlayerStateService.SetClassJobId(8);

        Assert.Equal(CrafterJobDetectionState.Crafter, viewModel.DetectionState);
        Assert.Equal(8u, viewModel.SelectedCrafterJob?.Job?.ClassJobId);
        Assert.Equal(XivLinker.Domain.Models.CrafterJobs.All.Count, viewModel.DisplayedCrafterJobs.Count);
        Assert.False(viewModel.CanChangeCrafterJob);
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

    private class FakeCurrentPlayerStateService : IOverlayPluginCurrentPlayerStateService
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

        public CurrentPlayerState CurrentState { get; protected set; }
    }

    private sealed class MutableCurrentPlayerStateService : IOverlayPluginCurrentPlayerStateService
    {
        public MutableCurrentPlayerStateService(CurrentPlayerState currentState)
        {
            CurrentState = currentState;
        }

        public event EventHandler? StateChanged;

        public CurrentPlayerState CurrentState { get; private set; }

        public void SetClassJobId(uint? classJobId)
        {
            CurrentState = new CurrentPlayerState
            {
                IsConnected = CurrentState.IsConnected,
                PlayerName = CurrentState.PlayerName,
                TerritoryTypeId = CurrentState.TerritoryTypeId,
                MapId = CurrentState.MapId,
                RawX = CurrentState.RawX,
                RawY = CurrentState.RawY,
                RawZ = CurrentState.RawZ,
                MapX = CurrentState.MapX,
                MapY = CurrentState.MapY,
                ClassJobId = classJobId,
                ClassJobName = CurrentState.ClassJobName,
                Level = CurrentState.Level,
                MapName = CurrentState.MapName,
                CoordinatesText = CurrentState.CoordinatesText,
                UpdatedAt = CurrentState.UpdatedAt,
                IssueMessage = CurrentState.IssueMessage,
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
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
