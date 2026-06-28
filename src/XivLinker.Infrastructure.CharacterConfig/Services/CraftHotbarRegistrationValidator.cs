using System.Buffers.Binary;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Models;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class CraftHotbarRegistrationValidator : ICraftHotbarRegistrationValidator
{
    private readonly ICharacterProfileStore characterProfileStore;

    public CraftHotbarRegistrationValidator(ICharacterProfileStore characterProfileStore)
    {
        this.characterProfileStore = characterProfileStore;
    }

    public Task<CraftSequenceValidationResult> ValidateAsync(
        CraftSequence sequence,
        CrafterJob crafterJob,
        CancellationToken cancellationToken = default)
    {
        CharacterData? characterData = characterProfileStore.SelectedCharacterData;
        if (characterData is null)
        {
            return Task.FromResult(CraftSequenceValidationResult.Failed(
                "キャラクター設定が読み込まれていないため、ホットバー登録を確認できません。"));
        }

        if (characterData.Errors.Count > 0)
        {
            return Task.FromResult(CraftSequenceValidationResult.Failed(
                "キャラクター設定の読み込みに失敗しているため、ホットバー登録を確認できません。"));
        }

        byte[]? rawBytes = characterData.HotbarAnalysisResult.RawBytes;
        if (characterData.HotbarAnalysisResult.Exists != true || rawBytes is null || rawBytes.Length == 0)
        {
            return Task.FromResult(CraftSequenceValidationResult.Failed(
                "現在選択中の HOTBAR.DAT を読み込めないため、ホットバー登録を確認できません。"));
        }

        IReadOnlyList<CraftActionRequirement> requiredActions = ResolveRequirements(sequence);
        if (requiredActions.Count == 0)
        {
            return Task.FromResult(new CraftSequenceValidationResult());
        }

        byte[] xorBytes = DecodeWithXor(rawBytes, 0x31);
        HashSet<CraftActionId> registeredActions = ResolveRegisteredActions(rawBytes, xorBytes, crafterJob);
        if (registeredActions.Count == 0)
        {
            return Task.FromResult(CraftSequenceValidationResult.Failed(
                "現在選択中のホットバー情報を読み取れないため、シーケンスを実行できません。"));
        }

        CraftActionRequirement[] missingActions = requiredActions
            .Where(action => !registeredActions.Contains(action.ActionId))
            .ToArray();

        return Task.FromResult(new CraftSequenceValidationResult
        {
            MissingActions = missingActions,
        });
    }

    private static IReadOnlyList<CraftActionRequirement> ResolveRequirements(CraftSequence sequence)
    {
        return sequence.Steps
            .Select(step => step.ActionId)
            .Where(actionId => !string.IsNullOrWhiteSpace(actionId.Value))
            .Distinct()
            .Select(actionId =>
            {
                if (CraftActionCatalog.TryGet(actionId, out CraftActionDefinition? definition) && definition is not null)
                {
                    return new CraftActionRequirement(actionId, definition.DisplayName);
                }

                return new CraftActionRequirement(actionId, $"未定義アクション / {actionId.Value}");
            })
            .ToArray();
    }

    private static HashSet<CraftActionId> ResolveRegisteredActions(
        byte[] rawBytes,
        byte[] xorBytes,
        CrafterJob crafterJob)
    {
        var registered = new HashSet<CraftActionId>();

        foreach (CraftActionDefinition definition in CraftActionCatalog.GetAll())
        {
            CrafterActionVariant? variant = definition.Variants.FirstOrDefault(item => item.ClassJobRowId == crafterJob.ClassJobId);
            if (variant is null)
            {
                continue;
            }

            if (ContainsUInt32(rawBytes, variant.LuminaRowId) || ContainsUInt32(xorBytes, variant.LuminaRowId))
            {
                registered.Add(definition.ActionId);
            }
        }

        return registered;
    }

    private static bool ContainsUInt32(byte[] source, uint value)
    {
        if (source.Length < sizeof(uint))
        {
            return false;
        }

        for (int index = 0; index <= source.Length - sizeof(uint); index++)
        {
            uint candidate = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(index, sizeof(uint)));
            if (candidate == value)
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] DecodeWithXor(byte[] source, byte key)
    {
        byte[] decoded = new byte[source.Length];

        for (int index = 0; index < source.Length; index++)
        {
            decoded[index] = (byte)(source[index] ^ key);
        }

        return decoded;
    }
}
