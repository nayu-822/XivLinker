using System.Text.Json;
using Microsoft.Extensions.Logging;
using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Application.Services;

public sealed class CraftSequenceStore : ICraftSequenceStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly List<CraftSequence> sequences = [];
    private readonly IAppDataPathService appDataPathService;
    private readonly ILogger<CraftSequenceStore> logger;

    public CraftSequenceStore(
        IAppDataPathService appDataPathService,
        ILogger<CraftSequenceStore> logger)
    {
        this.appDataPathService = appDataPathService;
        this.logger = logger;
        LoadFromDisk();
    }

    public IReadOnlyList<CraftSequence> GetAll()
    {
        return sequences
            .OrderByDescending(static sequence => sequence.UpdatedAt)
            .Select(Clone)
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
        }
        else
        {
            sequences.Add(clone);
        }

        Persist();
    }

    public void Delete(Guid sequenceId)
    {
        _ = sequences.RemoveAll(sequence => sequence.SequenceId == sequenceId);
        Persist();
    }

    private void LoadFromDisk()
    {
        string path = appDataPathService.CraftSequencesFilePath;
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            CraftSequenceStoreDocument? document = JsonSerializer.Deserialize<CraftSequenceStoreDocument>(json, JsonOptions);
            if (document?.Sequences is null)
            {
                return;
            }

            sequences.Clear();
            sequences.AddRange(document.Sequences.Select(ToModel));
        }
        catch (JsonException exception)
        {
            BackupBrokenFile(path);
            logger.LogError(exception, "Failed to parse craft sequence store file.");
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Failed to read craft sequence store file.");
        }
    }

    private void Persist()
    {
        try
        {
            var document = new CraftSequenceStoreDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Sequences = sequences.Select(FromModel).ToArray(),
            };

            WriteAtomically(appDataPathService.CraftSequencesFilePath, JsonSerializer.Serialize(document, JsonOptions));
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Failed to persist craft sequences.");
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception, "Failed to persist craft sequences.");
        }
    }

    private void BackupBrokenFile(string path)
    {
        try
        {
            string backupPath = $"{path}.{DateTimeOffset.Now:yyyyMMddHHmmss}.bak";
            File.Copy(path, backupPath, overwrite: true);
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Failed to back up broken craft sequence store file.");
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.tmp");
        File.WriteAllText(tempPath, content);

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
            return;
        }

        File.Move(tempPath, path);
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
                })
                .ToArray(),
        };
    }

    private static CraftSequenceDocument FromModel(CraftSequence sequence)
    {
        return new CraftSequenceDocument
        {
            SequenceId = sequence.SequenceId,
            Name = sequence.Name,
            UpdatedAt = sequence.UpdatedAt,
            Steps = sequence.Steps
                .Select(static step => new CraftSequenceStepDocument
                {
                    ActionId = step.ActionId.Value,
                })
                .ToArray(),
        };
    }

    private static CraftSequence ToModel(CraftSequenceDocument sequence)
    {
        return new CraftSequence
        {
            SequenceId = sequence.SequenceId,
            Name = sequence.Name ?? string.Empty,
            UpdatedAt = sequence.UpdatedAt,
            Steps = sequence.Steps?
                .Select(static step => new CraftSequenceStep
                {
                    ActionId = new CraftActionId(step.ActionId),
                })
                .ToArray() ?? [],
        };
    }

    private sealed class CraftSequenceStoreDocument
    {
        public int SchemaVersion { get; init; }

        public CraftSequenceDocument[] Sequences { get; init; } = [];
    }

    private sealed class CraftSequenceDocument
    {
        public Guid SequenceId { get; init; }

        public string? Name { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }

        public CraftSequenceStepDocument[]? Steps { get; init; }
    }

    private sealed class CraftSequenceStepDocument
    {
        public string ActionId { get; init; } = string.Empty;
    }
}
