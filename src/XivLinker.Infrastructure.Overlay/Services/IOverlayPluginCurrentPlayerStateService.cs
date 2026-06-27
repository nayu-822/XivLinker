using XivLinker.Infrastructure.Overlay.Models;

namespace XivLinker.Infrastructure.Overlay.Services;

public interface IOverlayPluginCurrentPlayerStateService
{
    event EventHandler? StateChanged;

    CurrentPlayerState CurrentState { get; }
}
