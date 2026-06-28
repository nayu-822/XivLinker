using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.App.Services;
using XivLinker.App.ViewModels;
using XivLinker.Application.Abstractions;
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
                Name = "削除テスト",
                Steps =
                [
                    new CraftSequenceStep
                    {
                        ActionId = CraftActionId.BasicSynthesis,
                    },
                ],
            };

            store.Save(sequence);

            var viewModel = new AutoCraftSequenceListViewModel(
                store,
                _ => Task.CompletedTask);

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

        var overlayWindowService = new TestOverlayWindowService();
        var actionExecutor = new CountingAutoCraftActionExecutor();
        var executionService = new AutoCraftExecutionService(
            overlayWindowService,
            actionExecutor,
            new AppEventLogViewModel(),
            NullLogger<AutoCraftExecutionService>.Instance);

        var viewModel = new AutoCraftSequenceListViewModel(
            store,
            _ => Task.CompletedTask,
            () => null,
            () => true,
            new PassThroughCraftHotbarRegistrationValidator(),
            overlayWindowService,
            executionService);

        viewModel.Refresh();
        CraftSequenceSummaryViewModel summary = Assert.Single(viewModel.Sequences);

        await viewModel.RunSequenceCommand.ExecuteAsync(summary);

        Assert.Equal("自動クラフト", overlayWindowService.LastTitle);
        Assert.Equal("現在のジョブがクラフター以外のため、このシーケンスは実行できません。", overlayWindowService.LastMessage);
        Assert.Equal(0, actionExecutor.ExecuteCount);
        Assert.False(executionService.IsRunning);
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

    private sealed class PassThroughCraftHotbarRegistrationValidator : ICraftHotbarRegistrationValidator
    {
        public Task<XivLinker.Application.Models.CraftSequenceValidationResult> ValidateAsync(
            CraftSequence sequence,
            CrafterJob crafterJob,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new XivLinker.Application.Models.CraftSequenceValidationResult());
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

        public override Task ShowMessageAsync(string title, string message)
        {
            LastTitle = title;
            LastMessage = message;
            return Task.CompletedTask;
        }

        public override Task<int?> ShowRunOptionsAsync(string sequenceName)
        {
            return Task.FromResult<int?>(1);
        }
    }
}
