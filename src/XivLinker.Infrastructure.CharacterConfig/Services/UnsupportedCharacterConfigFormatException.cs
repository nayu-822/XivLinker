namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class UnsupportedCharacterConfigFormatException : Exception
{
    public UnsupportedCharacterConfigFormatException(string message)
        : base(message)
    {
    }
}
