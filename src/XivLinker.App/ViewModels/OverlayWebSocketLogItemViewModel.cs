using XivLinker.Infrastructure.Overlay.Models;

namespace XivLinker.App.ViewModels;

public sealed class OverlayWebSocketLogItemViewModel
{
    public OverlayWebSocketLogItemViewModel(OverlayWebSocketCommunicationLogEntry entry)
    {
        Timestamp = entry.Timestamp.LocalDateTime;
        Direction = entry.Direction;
        Kind = entry.Kind;
        Name = string.IsNullOrWhiteSpace(entry.Name) ? "-" : entry.Name;
        SequenceNumber = entry.SequenceNumber;
        RawJson = entry.RawJson;
    }

    public DateTime Timestamp { get; }

    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");

    public string Direction { get; }

    public string Kind { get; }

    public string Name { get; }

    public long? SequenceNumber { get; }

    public string SequenceNumberText => SequenceNumber?.ToString() ?? "-";

    public string RawJson { get; }
}
