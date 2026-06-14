using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models;

namespace XivLinker.Application.Services;

public sealed class CraftSequenceStore : ICraftSequenceStore
{
    private readonly List<CraftSequence> sequences =
    [
        new CraftSequence
        {
            SequenceId = Guid.Parse("E3C395F2-5759-45FF-8EC0-78E89BB8290D"),
            Name = "収集品の基本回し",
            UpdatedAt = DateTimeOffset.Now.AddDays(-1),
            Steps =
            [
                new CraftSequenceStep { ActionName = "作業", WaitMilliseconds = 2500 },
                new CraftSequenceStep { ActionName = "中級加工", WaitMilliseconds = 2500 },
                new CraftSequenceStep { ActionName = "模範作業", WaitMilliseconds = 2500 },
            ],
        },
        new CraftSequence
        {
            SequenceId = Guid.Parse("65CDA5E8-122A-4A49-98BC-80B484B6B7D1"),
            Name = "耐久40向け簡易回し",
            UpdatedAt = DateTimeOffset.Now.AddHours(-6),
            Steps =
            [
                new CraftSequenceStep { ActionName = "倹約", WaitMilliseconds = 2500 },
                new CraftSequenceStep { ActionName = "加工", WaitMilliseconds = 2500 },
            ],
        },
    ];

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
                    ActionName = step.ActionName,
                    WaitMilliseconds = step.WaitMilliseconds,
                })
                .ToArray(),
        };
    }
}
