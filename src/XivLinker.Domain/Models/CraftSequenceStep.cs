namespace XivLinker.Domain.Models;

public sealed class CraftSequenceStep
{
    public string ActionName { get; set; } = string.Empty;

    public int WaitMilliseconds
    {
        get; set;
    }
}
