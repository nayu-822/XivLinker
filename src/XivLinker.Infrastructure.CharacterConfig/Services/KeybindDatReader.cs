using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed partial class KeybindDatReader
{
    private const byte XorKey = 0x73;
    private readonly ILogger<KeybindDatReader> logger;

    public KeybindDatReader(ILogger<KeybindDatReader>? logger = null)
    {
        this.logger = logger ?? NullLogger<KeybindDatReader>.Instance;
    }

    public IReadOnlyList<KeybindEntry> Read(byte[] keybindDatBytes)
    {
        ArgumentNullException.ThrowIfNull(keybindDatBytes);

        byte[] content = DatFileContentReader.ReadDecodedContent(
            keybindDatBytes,
            xorKey: XorKey,
            fileName: "KEYBIND.DAT");

        logger.LogInformation(
            "KEYBIND.DAT decoded. DecodedLength: {Length}, FirstBytes: {FirstBytes}",
            content.Length,
            ToHex(content, 64));

        var entries = new List<KeybindEntry>();
        int offset = 0;

        while (offset < content.Length)
        {
            try
            {
                string command = ReadSectionString(content, ref offset, expectedType: 'T');
                string keyString = ReadSectionString(content, ref offset, expectedType: 'C');

                KeybindEntry entry = ParseKeybindEntry(command, keyString);
                if (string.IsNullOrWhiteSpace(entry.Command))
                {
                    continue;
                }

                entries.Add(entry);
                logger.LogInformation(
                    "KEYBIND command loaded. Command: {Command}, Primary: {Primary}, Secondary: {Secondary}",
                    entry.Command,
                    entry.Primary?.DisplayText,
                    entry.Secondary?.DisplayText);
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning(
                    exception,
                    "KEYBIND.DAT section parse failed. Offset: {Offset}, Remaining: {Remaining}, NearBytes: {NearBytes}",
                    offset,
                    content.Length - offset,
                    ToHex(content.AsSpan(offset, Math.Min(64, content.Length - offset)).ToArray(), 64));
                throw;
            }
        }

        return entries;
    }

    public IReadOnlyList<HotbarSlotKeyBinding> ReadHotbarKeyBindings(byte[] keybindDatBytes)
    {
        IReadOnlyList<KeybindEntry> entries = Read(keybindDatBytes);
        var result = new List<HotbarSlotKeyBinding>();

        foreach (KeybindEntry entry in entries)
        {
            if (!TryParseHotbarCommand(entry.Command, out int hotbarNumber, out int slotNumber))
            {
                continue;
            }

            KeybindGesture? gesture = entry.Primary ?? entry.Secondary;
            if (gesture is null || gesture.Keys.Count == 0)
            {
                continue;
            }

            logger.LogInformation(
                "Hotbar keybind resolved. Command: {Command}, Hotbar: {Hotbar}, Slot: {Slot}, Key: {Key}",
                entry.Command,
                hotbarNumber,
                slotNumber,
                gesture.DisplayText);

            result.Add(new HotbarSlotKeyBinding(
                hotbarNumber,
                slotNumber,
                entry.Command,
                gesture.DisplayText,
                gesture.Keys));
        }

        if (result.Count == 0)
        {
            logger.LogWarning(
                "No hotbar keybind commands were resolved from KEYBIND.DAT. Commands: {Commands}",
                string.Join(", ", entries.Select(static entry => entry.Command)));
        }

        return result;
    }

    private static string ReadSectionString(
        byte[] content,
        ref int offset,
        char expectedType)
    {
        if (offset + 3 > content.Length)
        {
            throw new InvalidDataException("KEYBIND.DAT のsectionヘッダーが不正です。");
        }

        char type = (char)content[offset];
        ushort size = BitConverter.ToUInt16(content, offset + 1);
        offset += 3;

        if (type != expectedType)
        {
            throw new InvalidDataException(
                $"KEYBIND.DAT のsection typeが不正です。expected={expectedType}, actual={type}");
        }

        if (size == 0 || offset + size > content.Length)
        {
            throw new InvalidDataException(
                $"KEYBIND.DAT のsection sizeが不正です。type={type}, size={size}");
        }

        ReadOnlySpan<byte> data = content.AsSpan(offset, size);
        offset += size;

        if (data.Length > 0 && data[^1] == 0)
        {
            data = data[..^1];
        }

        return Encoding.UTF8.GetString(data);
    }

    private static KeybindEntry ParseKeybindEntry(string command, string keyString)
    {
        string[] parts = keyString.Split(',', StringSplitOptions.None);

        KeybindGesture? primary = parts.Length > 0
            ? ParseGesture(parts[0])
            : null;

        KeybindGesture? secondary = parts.Length > 1
            ? ParseGesture(parts[1])
            : null;

        return new KeybindEntry(command.Trim(), primary, secondary);
    }

    private static KeybindGesture? ParseGesture(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string[] parts = text.Split('.', StringSplitOptions.None);
        string key = parts.Length > 0 ? parts[0] : string.Empty;
        string modifier = parts.Length > 1 ? parts[1] : string.Empty;

        if (string.IsNullOrWhiteSpace(key) || key == "0")
        {
            return null;
        }

        List<string> keys = [];

        foreach (string modifierKey in NormalizeModifier(modifier))
        {
            keys.Add(modifierKey);
        }

        keys.Add(NormalizeKey(key));

        string displayText = string.Join("+", keys);
        return new KeybindGesture(key, modifier, displayText, keys);
    }

    private static IEnumerable<string> NormalizeModifier(string modifier)
    {
        if (string.IsNullOrWhiteSpace(modifier) || modifier == "0")
        {
            yield break;
        }

        if (!int.TryParse(modifier, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            yield return modifier;
            yield break;
        }

        if ((value & 1) != 0)
        {
            yield return "Ctrl";
        }

        if ((value & 2) != 0)
        {
            yield return "Alt";
        }

        if ((value & 4) != 0)
        {
            yield return "Shift";
        }
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim() switch
        {
            " " => "Space",
            _ => key.Trim(),
        };
    }

    private static bool TryParseHotbarCommand(
        string command,
        out int hotbarNumber,
        out int slotNumber)
    {
        hotbarNumber = 0;
        slotNumber = 0;

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

            hotbarNumber = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            slotNumber = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

            return hotbarNumber is >= 1 and <= 10
                && slotNumber is >= 1 and <= 12;
        }

        return false;
    }

    private static string ToHex(byte[] bytes, int maxLength)
    {
        return Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, maxLength)));
    }
}
