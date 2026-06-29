using XivLinker.Application.Logging;

namespace XivLinker.App.ViewModels;

public sealed record XivLinkerLogLevelOptionViewModel(
    XivLinkerLogLevel Value,
    string DisplayName,
    string Description)
{
    public override string ToString()
    {
        return DisplayName;
    }
}
