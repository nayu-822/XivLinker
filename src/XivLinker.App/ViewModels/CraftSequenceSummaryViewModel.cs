using XivLinker.Domain.Models;

namespace XivLinker.App.ViewModels;

public sealed class CraftSequenceSummaryViewModel
{
    public Guid SequenceId
    {
        get; init;
    }

    public string Name { get; init; } = string.Empty;

    public int StepCount
    {
        get; init;
    }

    public string UpdatedAtText { get; init; } = "-";

    public string Summary => $"ステップ数: {StepCount}";

    public static CraftSequenceSummaryViewModel FromModel(CraftSequence sequence)
    {
        return new CraftSequenceSummaryViewModel
        {
            SequenceId = sequence.SequenceId,
            Name = sequence.Name,
            StepCount = sequence.Steps.Count,
            UpdatedAtText = sequence.UpdatedAt.ToString("yyyy/MM/dd HH:mm"),
        };
    }
}
