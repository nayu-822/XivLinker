namespace XivLinker.Infrastructure.Overlay.Models;

public sealed class OverlayWebSocketCommunicationLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string Direction { get; init; }

    public required string Kind { get; init; }

    public string Name { get; init; } = string.Empty;

    public long? SequenceNumber { get; init; }

    public required string RawJson { get; init; }
}
