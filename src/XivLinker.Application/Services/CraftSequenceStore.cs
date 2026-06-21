using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models;

namespace XivLinker.Application.Services;

public sealed class CraftSequenceStore : ICraftSequenceStore
{
    private readonly List<CraftSequence> sequences = [];

    public IReadOnlyList<CraftSequence> GetAll()
    {
        return sequences
            .OrderByDescending(static sequence => sequence.UpdatedAt)
            .ToArray();
    }

    public CraftSequence? Find(Guid sequenceId)
    {
        CraftSequence? sequence = sequences.FirstOrDefault(sequence => sequence.SequenceId == sequenceId);
        return sequence is null ? null : Clone(sequence);
    }

    public void Save(CraftSequence sequence)
    {
        CraftSequence clone = Clone(sequence);
        clone.UpdatedAt = DateTimeOffset.Now;

        int index = sequences.FindIndex(existing => existing.SequenceId == clone.SequenceId);
        if (index >= 0)
        {
            sequences[index] = clone;
            return;
        }

        sequences.Add(clone);
    }

    public void Delete(Guid sequenceId)
    {
        _ = sequences.RemoveAll(sequence => sequence.SequenceId == sequenceId);
    }

    private static CraftSequence Clone(CraftSequence sequence)
    {
        return new CraftSequence
        {
            SequenceId = sequence.SequenceId,
            Name = sequence.Name,
            UpdatedAt = sequence.UpdatedAt,
            Steps = sequence.Steps
                .Select(static step => new CraftSequenceStep
                {
                    ActionId = step.ActionId,
                    WaitMilliseconds = step.WaitMilliseconds,
                })
                .ToArray(),
        };
    }
}
