using Microsoft.Extensions.Logging;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Models;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class CraftActionIdResolver : ICraftActionIdResolver
{
    private readonly ILogger<CraftActionIdResolver> logger;

    public CraftActionIdResolver(ILogger<CraftActionIdResolver> logger)
    {
        this.logger = logger;
    }

    public Task<IReadOnlyList<CraftActionRequirement>> ResolveRequiredActionsAsync(
        CraftSequence sequence,
        CrafterJob crafterJob,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(crafterJob);

        IReadOnlyList<CraftActionRequirement> requirements = sequence.Steps
            .Select(static step => step.ActionId)
            .Where(static actionId => !string.IsNullOrWhiteSpace(actionId.Value))
            .Distinct()
            .Select(actionId => ResolveRequirement(actionId, crafterJob))
            .ToArray();

        return Task.FromResult(requirements);
    }

    private CraftActionRequirement ResolveRequirement(CraftActionId actionId, CrafterJob crafterJob)
    {
        if (!CraftActionCatalog.TryGet(actionId, out CraftActionDefinition? definition) || definition is null)
        {
            var unresolvedRequirement = new CraftActionRequirement(actionId, 0, actionId.Value);

            LogResolution(crafterJob, unresolvedRequirement);
            return unresolvedRequirement;
        }

        CrafterActionVariant? variant = definition.Variants
            .FirstOrDefault(item => item.ClassJobRowId == crafterJob.ClassJobId);

        var requirement = new CraftActionRequirement(
            actionId,
            variant?.LuminaRowId ?? 0,
            definition.DisplayName);

        LogResolution(crafterJob, requirement);
        return requirement;
    }

    private void LogResolution(CrafterJob crafterJob, CraftActionRequirement requirement)
    {
        logger.LogInformation(
            "Craft action resolved for current job. Job: {Job}, Action: {ActionName}, CraftActionId: {CraftActionId}, LuminaActionId: {LuminaActionId}",
            crafterJob.Name,
            requirement.ActionName,
            requirement.ActionId,
            requirement.LuminaActionId);
    }
}
