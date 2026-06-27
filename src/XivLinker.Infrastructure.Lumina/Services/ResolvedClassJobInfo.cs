namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class ResolvedClassJobInfo
{
    public uint ClassJobId { get; init; }

    public string ClassJobName { get; init; } = string.Empty;

    public int? Level { get; init; }

    public string DisplayText { get; init; } = string.Empty;

    public string? IssueMessage { get; init; }
}
