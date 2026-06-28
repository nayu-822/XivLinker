using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public static class KeybindDisplayFormatter
{
    public static string Format(KeybindGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        return gesture.DisplayText;
    }

    public static IReadOnlyList<string> ToKeys(KeybindGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        return gesture.Keys;
    }
}
