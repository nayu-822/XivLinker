using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.App.Services;
using XivLinker.App.ViewModels;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Models;
using XivLinker.Application.Services;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Tests;

public sealed class AutoCraftSequenceListViewModelTests
{
    [Fact]
    public void DeleteSequenceCommand_RemovesSequenceFromStore()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "XivLinkerTests", Guid.NewGuid().ToString("N"));

        try
        {
            ICraftSequenceStore store = new CraftSequenceStore(
                new AppDataPathService(rootPath),
                NullLogger<CraftSequenceStore>.Instance);

            var sequence = new CraftSequence
            {
                SequenceId = Guid.NewGuid(),
                Name = "テスト",
                Steps =
                [
                    new CraftSequenceStep
                    {
                        ActionId = CraftActionId.BasicSynthesis,
                    },
                ],
            };

            store.Save(sequence);

            var viewModel = CreateViewModel(
                store,
                new TestOverlayWindowService(),
                new FakeCraftSequenceExecutionPreparer(new CraftSequenceExecutionPreparationResult()),
                new CountingAutoCraftActionExecutor(),
                () => CrafterJobs.Carpenter,
                () => false);

            viewModel.Refresh();
            CraftSequenceSummaryViewModel summary = Assert.Single(viewModel.Sequences);

            viewModel.DeleteSequenceCommand.Execute(summary);

            Assert.Empty(viewModel.Sequences);
            Assert.Null(store.Find(sequence.SequenceId));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunSequenceCommand_ShowsMessageAndDoesNotStart_WhenCurrentJobIsNonCrafter()
    {
        var store = new InMemoryCraftSequenceStore();
        CraftSequence sequence = CreateSequence();
        store.Save(sequence);

        var overlayWindowService = new TestOverlayWindowService();
        var actionExecutor = new CountingAutoCraftActionExecutor();
        var viewModel = CreateViewModel(
            store,
            overlayWindowService,
            new FakeCraftSequenceExecutionPreparer(new CraftSequenceExecutionPreparationResult()),
            actionExecutor,
            () => null,
            () => true);

        viewModel.Refresh();
        CraftSequenceSummaryViewModel summary = Assert.Single(viewModel.Sequences);

        await viewModel.RunSequenceCommand.ExecuteAsync(summary);

        Assert.Equal("自動クラフト", overlayWindowService.LastTitle);
        Assert.Equal("現在のジョブがクラフターではないため、このシーケンスは実行できません。", overlayWindowService.LastMessage);
        Assert.Equal(0, actionExecutor.ExecuteCount);
        Assert.Equal(0, overlayWindowService.RunOptionsShownCount);
    }

    [Fact]
    public async Task RunSequenceCommand_ShowsMessageAndDoesNotStart_WhenCrafterJobIsUnknown()
    {
        var store = new InMemoryCraftSequenceStore();
        CraftSequence sequence = CreateSequence();
        store.Save(sequence);

        var overlayWindowService = new TestOverlayWindowService();
        var actionExecutor = new CountingAutoCraftActionExecutor();
        var viewModel = CreateViewModel(
            store,
            overlayWindowService,
            new FakeCraftSequenceExecutionPreparer(new CraftSequenceExecutionPreparationResult()),
            actionExecutor,
            () => null,
            () => false);

        viewModel.Refresh();
        CraftSequenceSummaryViewModel summary = Assert.Single(viewModel.Sequences);

        await viewModel.RunSequenceCommand.ExecuteAsync(summary);

        Assert.Equal("自動クラフト", overlayWindowService.LastTitle);
        Assert.Equal("現在のジョブを自動判定できないため、クラフター職を選択してから実行してください。", overlayWindowService.LastMessage);
        Assert.Equal(0, actionExecutor.ExecuteCount);
        Assert.Equal(0, overlayWindowService.RunOptionsShownCount);
    }

    [Fact]
    public async Task RunSequenceCommand_ShowsPreparationFailureAndDoesNotStart_WhenActionsAreMissing()
    {
        var store = new InMemoryCraftSequenceStore();
        CraftSequence sequence = CreateSequence();
        store.Save(sequence);

        var overlayWindowService = new TestOverlayWindowService();
        var actionExecutor = new CountingAutoCraftActionExecutor();
        var preparer = new FakeCraftSequenceExecutionPreparer(new CraftSequenceExecutionPreparationResult
        {
            MissingActions =
            [
                new CraftActionRequirement(CraftActionId.BasicSynthesis, 1001, "作業"),
            ],
        });

        var viewModel = CreateViewModel(
            store,
            overlayWindowService,
            preparer,
            actionExecutor,
            () => CrafterJobs.Carpenter,
            () => false);

        viewModel.Refresh();
        CraftSequenceSummaryViewModel summary = Assert.Single(viewModel.Sequences);

        await viewModel.RunSequenceCommand.ExecuteAsync(summary);

        Assert.Equal(1, overlayWindowService.PreparationFailureShownCount);
        Assert.Single(overlayWindowService.LastMissingActions);
        Assert.Equal(0, overlayWindowService.RunOptionsShownCount);
        Assert.Equal(0, actionExecutor.ExecuteCount);
    }

    [Fact]
    public async Task RunSequenceCommand_ShowsErrorMessageAndDoesNotStart_WhenPreparationFails()
    {
        var store = new InMemoryCraftSequenceStore();
        CraftSequence sequence = CreateSequence();
        store.Save(sequence);

        var overlayWindowService = new TestOverlayWindowService();
        var actionExecutor = new CountingAutoCraftActionExecutor();
        var preparer = new FakeCraftSequenceExecutionPreparer(
            CraftSequenceExecutionPreparationResult.Failed("HOTBAR.DAT を読み込めません"));

        var viewModel = CreateViewModel(
            store,
            overlayWindowService,
            preparer,
            actionExecutor,
            () => CrafterJobs.Carpenter,
            () => false);

        viewModel.Refresh();
        CraftSequenceSummaryViewModel summary = Assert.Single(viewModel.Sequences);

        await viewModel.RunSequenceCommand.ExecuteAsync(summary);

        Assert.Equal("HOTBAR.DAT を読み込めません", overlayWindowService.LastMessage);
        Assert.Equal(0, overlayWindowService.RunOptionsShownCount);
        Assert.Equal(0, actionExecutor.ExecuteCount);
    }

    [Fact]
    public async Task RunSequenceCommand_Starts_WhenPreparationSucceeds()
    {
        var store = new InMemoryCraftSequenceStore();
        CraftSequence sequence = CreateSequence();
        store.Save(sequence);

        var overlayWindowService = new TestOverlayWindowService();
        var actionExecutor = new CountingAutoCraftActionExecutor();
        var preparer = new FakeCraftSequenceExecutionPreparer(new CraftSequenceExecutionPreparationResult
        {
            ActionKeyBindings =
            [
                new CraftActionKeyBinding(
                    CraftActionId.BasicSynthesis,
                    "作業",
                    1,
                    1,
                    "1",
                    ["1"]),
            ],
        });

        var viewModel = CreateViewModel(
            store,
            overlayWindowService,
            preparer,
            actionExecutor,
            () => CrafterJobs.Carpenter,
            () => false);

        viewModel.Refresh();
        CraftSequenceSummaryViewModel summary = Assert.Single(viewModel.Sequences);

        await viewModel.RunSequenceCommand.ExecuteAsync(summary);
        await Task.Delay(50);

        Assert.Equal(1, overlayWindowService.RunOptionsShownCount);
        Assert.Equal(1, actionExecutor.ExecuteCount);
    }

    private static CraftSequence CreateSequence()
    {
        return new CraftSequence
        {
            SequenceId = Guid.NewGuid(),
            Name = "テスト",
            Steps =
            [
                new CraftSequenceStep
                {
                    ActionId = CraftActionId.BasicSynthesis,
                },
            ],
        };
    }

    private static AutoCraftSequenceListViewModel CreateViewModel(
        ICraftSequenceStore store,
        TestOverlayWindowService overlayWindowService,
        ICraftSequenceExecutionPreparer preparer,
        CountingAutoCraftActionExecutor actionExecutor,
        Func<CrafterJob?> getSelectedCrafterJob,
        Func<bool> isCurrentJobNonCrafter)
    {
        EnsureWpfApplication();

        var executionService = new AutoCraftExecutionService(
            overlayWindowService,
            actionExecutor,
            new AppEventLogViewModel(),
            NullLogger<AutoCraftExecutionService>.Instance);

        return new AutoCraftSequenceListViewModel(
            store,
            _ => Task.CompletedTask,
            getSelectedCrafterJob,
            isCurrentJobNonCrafter,
            preparer,
            overlayWindowService,
            executionService);
    }

    private static void EnsureWpfApplication()
    {
        if (System.Windows.Application.Current is null)
        {
            _ = new System.Windows.Application();
        }
    }

    private sealed class InMemoryCraftSequenceStore : ICraftSequenceStore
    {
        private readonly Dictionary<Guid, CraftSequence> sequences = [];

        public IReadOnlyList<CraftSequence> GetAll() => sequences.Values.ToList();

        public CraftSequence? Find(Guid sequenceId) => sequences.GetValueOrDefault(sequenceId);

        public void Save(CraftSequence sequence)
        {
            sequences[sequence.SequenceId] = sequence;
        }

        public void Delete(Guid sequenceId)
        {
            sequences.Remove(sequenceId);
        }
    }

    private sealed class FakeCraftSequenceExecutionPreparer : ICraftSequenceExecutionPreparer
    {
        private readonly CraftSequenceExecutionPreparationResult result;

        public FakeCraftSequenceExecutionPreparer(CraftSequenceExecutionPreparationResult result)
        {
            this.result = result;
        }

        public Task<CraftSequenceExecutionPreparationResult> PrepareAsync(
            CraftSequence sequence,
            CrafterJob crafterJob,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class CountingAutoCraftActionExecutor : IAutoCraftActionExecutor
    {
        public int ExecuteCount { get; private set; }

        public Task ExecuteAsync(
            CraftSequence sequence,
            int runCount,
            Action<string>? reportStatus,
            CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestOverlayWindowService : OverlayWindowService
    {
        public string? LastTitle { get; private set; }

        public string? LastMessage { get; private set; }

        public int RunOptionsShownCount { get; private set; }

        public int PreparationFailureShownCount { get; private set; }

        public IReadOnlyList<CraftActionRequirement> LastMissingActions { get; private set; } = [];

        public IReadOnlyList<CraftActionRequirement> LastUnboundActions { get; private set; } = [];

        public override Task ShowMessageAsync(string title, string message)
        {
            LastTitle = title;
            LastMessage = message;
            return Task.CompletedTask;
        }

        public override Task<int?> ShowRunOptionsAsync(string sequenceName)
        {
            RunOptionsShownCount++;
            return Task.FromResult<int?>(1);
        }

        public override Task ShowCraftExecutionPreparationFailedAsync(
            string sequenceName,
            string crafterJobName,
            IReadOnlyList<CraftActionRequirement> missingActions,
            IReadOnlyList<CraftActionRequirement> unboundActions)
        {
            PreparationFailureShownCount++;
            LastMissingActions = missingActions;
            LastUnboundActions = unboundActions;
            return Task.CompletedTask;
        }

        public override void ShowRunOverlay(AutoCraftRunOverlayViewModel viewModel)
        {
        }

        public override void CloseRunOverlay()
        {
        }

        public override void HideMainWindow()
        {
        }

        public override void ShowMainWindow()
        {
        }

        public override void ShowMainWindow(bool activate)
        {
        }
    }
}
