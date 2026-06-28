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

        var viewModel = new AutoCraftViewModel(
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

        Assert.True(viewModel.IsCrafterJobAutoDetected);
        Assert.False(viewModel.CanChangeCrafterJob);
        Assert.Equal(8u, viewModel.SelectedCrafterJob?.Job.ClassJobId);
    }

    [Fact]
    public void Constructor_AllowsManualSelection_WhenCurrentJobIsNotCrafter()
    {
        var currentPlayerStateService = new FakeCurrentPlayerStateService(new CurrentPlayerState
        {
            ClassJobId = 1,
        });

        var viewModel = new AutoCraftViewModel(
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

        Assert.False(viewModel.IsCrafterJobAutoDetected);
        Assert.True(viewModel.CanChangeCrafterJob);
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
