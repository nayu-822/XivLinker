using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed partial class KeybindDatReader
{
    private const byte XorKey = 0x73;

    private static readonly IReadOnlyDictionary<string, (byte HotbarId, byte SlotId)> HotbarCommandMap =
        CreateHotbarCommandMap();

    private readonly ILogger<KeybindDatReader> logger;

    public KeybindDatReader(ILogger<KeybindDatReader>? logger = null)
    {
        this.logger = logger ?? NullLogger<KeybindDatReader>.Instance;
    }

    public IReadOnlyList<KeybindEntry> Read(byte[] keybindDatBytes)
    {
        ArgumentNullException.ThrowIfNull(keybindDatBytes);

        byte[] decodedBody = DatFileContentReader.ReadDecodedContent(
            keybindDatBytes,
            xorKey: XorKey,
            fileName: "KEYBIND.DAT");

        IReadOnlyList<KeybindSectionEntry> sections = ParseSections(decodedBody);
        if (sections.Count % 2 != 0)
        {
            throw new InvalidDataException("KEYBIND.DAT の section 数が奇数です。command/binding ペアを解釈できません。");
        }

        var entries = new List<KeybindEntry>();

        for (int index = 0; index < sections.Count; index += 2)
        {
            KeybindSectionEntry commandSection = sections[index];
            KeybindSectionEntry bindingSection = sections[index + 1];

            (KeybindGesture? primary, KeybindGesture? secondary) = ParseBindingText(bindingSection.Content);

            var entry = new KeybindEntry(
                commandSection.Content,
                primary,
                secondary)
            {
                CommandSectionType = commandSection.TagCharacter,
                BindingSectionType = bindingSection.TagCharacter,
                RawBindingText = bindingSection.Content,
            };

            entries.Add(entry);

            logger.LogDebug(
                "KEYBIND command loaded. Command: {Command}, Primary: {Primary}, Secondary: {Secondary}, RawBinding: {RawBinding}, CommandSectionType: {CommandSectionType}, BindingSectionType: {BindingSectionType}",
                entry.Command,
                KeybindDisplayFormatter.Format(entry.Primary),
                KeybindDisplayFormatter.Format(entry.Secondary),
                entry.RawBindingText,
                entry.CommandSectionType,
                entry.BindingSectionType);
        }

        return entries;
    }

    public static bool TryResolveHotbarCommand(
        string command,
        out byte hotbarId,
        out byte slotId)
    {
        hotbarId = 0;
        slotId = 0;

        if (HotbarCommandMap.TryGetValue(command.Trim(), out (byte HotbarId, byte SlotId) mapped))
        {
            hotbarId = mapped.HotbarId;
            slotId = mapped.SlotId;
            return true;
        }

        string normalized = command.Trim();

        if (normalized.Contains("CROSS", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("XHB", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CHOTBAR", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] patterns =
        [
            @"HOTBAR[_\s]*(\d+)[_\s]*(\d+)",
            @"HBAR[_\s]*(\d+)[_\s]*(\d+)",
            @"HOTBAR(\d+)[_\s]*(\d+)",
            @"HOTBAR_SLOT[_\s]*(\d+)[_\s]*(\d+)",
        ];

        foreach (string pattern in patterns)
        {
            Match match = Regex.Match(normalized, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            if (!byte.TryParse(match.Groups[1].Value, out hotbarId)
                || !byte.TryParse(match.Groups[2].Value, out slotId))
            {
                return false;
            }

            return hotbarId <= 9
                && slotId <= 11;
        }

        return false;
    }

    private static IReadOnlyDictionary<string, (byte HotbarId, byte SlotId)> CreateHotbarCommandMap()
    {
        var map = new Dictionary<string, (byte HotbarId, byte SlotId)>(StringComparer.OrdinalIgnoreCase);

        for (byte hotbar = 0; hotbar < 10; hotbar++)
        {
            for (byte slot = 0; slot < 12; slot++)
            {
                map[$"HOTBAR_{hotbar}_{slot}"] = (hotbar, slot);
            }
        }

        for (byte displayHotbar = 1; displayHotbar <= 10; displayHotbar++)
        {
            for (byte displaySlot = 1; displaySlot <= 12; displaySlot++)
            {
                map.TryAdd(
                    $"HOTBAR_{displayHotbar}_{displaySlot}",
                    ((byte)(displayHotbar - 1), (byte)(displaySlot - 1)));
            }
        }

        return map;
    }

    private IReadOnlyList<KeybindSectionEntry> ParseSections(byte[] decodedBody)
    {
        var sections = new List<KeybindSectionEntry>();
        int offset = 0;
        int index = 0;

        while (offset < decodedBody.Length)
        {
            if (offset == decodedBody.Length - 1 && decodedBody[offset] == 0)
            {
                break;
            }

            sections.Add(ReadSection(decodedBody, index, ref offset));
            index++;
        }

        return sections;
    }

    private static (KeybindGesture? Primary, KeybindGesture? Secondary) ParseBindingText(string bindingText)
    {
        string[] parts = bindingText.Split(',', StringSplitOptions.None);

        KeybindGesture? primary = parts.Length > 0 ? ParseGesture(parts[0]) : null;
        KeybindGesture? secondary = parts.Length > 1 ? ParseGesture(parts[1]) : null;

        return (primary, secondary);
    }

    private static KeybindGesture? ParseGesture(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string[] parts = text.Split('.', StringSplitOptions.None);
        string keyCode = parts.Length > 0 ? parts[0].Trim() : string.Empty;
        string modifierCode = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(keyCode) || keyCode == "00" || keyCode == "0")
        {
            return null;
        }

        return new KeybindGesture(keyCode, string.IsNullOrWhiteSpace(modifierCode) ? "00" : modifierCode);
    }

    private static KeybindSectionEntry ReadSection(
        byte[] decodedBody,
        int index,
        ref int offset)
    {
        if (offset + 3 > decodedBody.Length)
        {
            throw new InvalidDataException("KEYBIND.DAT の section header が不正です。");
        }

        byte tag = decodedBody[offset];
        ushort declaredSize = (ushort)(decodedBody[offset + 1] | (decodedBody[offset + 2] << 8));

        if (declaredSize == 0)
        {
            throw new InvalidDataException($"KEYBIND.DAT に zero-sized section があります。Offset={offset}");
        }

        int payloadOffset = offset + 3;
        int payloadEndExclusive = payloadOffset + declaredSize;

        if (payloadEndExclusive > decodedBody.Length)
        {
            throw new InvalidDataException($"KEYBIND.DAT の section が終端を超えています。Offset={offset}");
        }

        byte[] payloadBytes = decodedBody[payloadOffset..payloadEndExclusive];

        if (payloadBytes.Length == 0 || payloadBytes[^1] != 0)
        {
            throw new InvalidDataException($"KEYBIND.DAT の section に null terminator がありません。Offset={offset}");
        }

        string content = Encoding.UTF8.GetString(payloadBytes, 0, payloadBytes.Length - 1);

        var section = new KeybindSectionEntry
        {
            Index = index,
            Offset = offset,
            Tag = tag,
            DeclaredSize = declaredSize,
            Content = content,
            PayloadBytes = payloadBytes,
        };

        offset = payloadEndExclusive;
        return section;
    }

    private sealed class KeybindSectionEntry
    {
        public int Index { get; init; }

        public int Offset { get; init; }

        public byte Tag { get; init; }

        public char TagCharacter => (char)Tag;

        public ushort DeclaredSize { get; init; }

        public string Content { get; init; } = string.Empty;

        public byte[] PayloadBytes { get; init; } = [];
    }
}
