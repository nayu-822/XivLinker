using XivLinker.Domain.Models;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

internal static class HotbarGroupDefinitions
{
    private const byte SharedGroupId = 0;

    private static readonly IReadOnlyDictionary<byte, HotbarGroupDefinition> Definitions =
        CrafterJobs.All.ToDictionary(
            static job => checked((byte)job.ClassJobId),
            static job => new HotbarGroupDefinition(
                checked((byte)job.ClassJobId),
                job.ClassJobId,
                job.Name));

    public static bool IsShared(byte groupId)
    {
        return groupId == SharedGroupId;
    }

    public static bool TryGetDefinition(byte groupId, out HotbarGroupDefinition? definition)
    {
        if (Definitions.TryGetValue(groupId, out HotbarGroupDefinition? found))
        {
            definition = found;
            return true;
        }

        definition = null;
        return false;
    }
}
