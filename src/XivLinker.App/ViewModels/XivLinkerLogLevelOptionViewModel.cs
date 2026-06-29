using XivLinker.Application.Logging;

namespace XivLinker.App.ViewModels;

public sealed record XivLinkerLogLevelOptionViewModel(
    XivLinkerLogLevel Value,
    string DisplayName,
    string Description);
