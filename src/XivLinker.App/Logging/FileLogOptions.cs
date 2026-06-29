namespace XivLinker.App.Logging;

public sealed class FileLogOptions
{
    public string LogsPath { get; init; } = string.Empty;

    public string FilePrefix { get; init; } = "xivlinker";
}
