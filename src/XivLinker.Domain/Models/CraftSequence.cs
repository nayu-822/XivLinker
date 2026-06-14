namespace XivLinker.Domain.Models;

public sealed class CraftSequence
{
    public Guid SequenceId
    {
        get; init;
    }

    public string Name { get; set; } = string.Empty;

    public IReadOnlyList<CraftSequenceStep> Steps { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
