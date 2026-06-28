using XivLinker.Infrastructure.Overlay.Models;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.Tests;

public sealed class OverlayWebSocketCommunicationLogEntryFactoryTests
{
    [Fact]
    public void Create_RequestJson_ParsesRequestMetadata()
    {
        const string json = """
            {
              "type": "request",
              "call": "subscribe",
              "rseq": 12,
              "events": ["ChangeZone"]
            }
            """;

        OverlayWebSocketCommunicationLogEntry entry =
            OverlayWebSocketCommunicationLogEntryFactory.Create("送信", json);

        Assert.Equal("送信", entry.Direction);
        Assert.Equal("request", entry.Kind);
        Assert.Equal("subscribe", entry.Name);
        Assert.Equal(12L, entry.SequenceNumber);
    }

    [Fact]
    public void Create_ResponseJson_UsesFallbackName()
    {
        const string json = """
            {
              "type": "response",
              "rseq": 18,
              "combatants": []
            }
            """;

        OverlayWebSocketCommunicationLogEntry entry =
            OverlayWebSocketCommunicationLogEntryFactory.Create("受信", json, "getCombatants");

        Assert.Equal("response", entry.Kind);
        Assert.Equal("getCombatants", entry.Name);
        Assert.Equal(18L, entry.SequenceNumber);
    }

    [Fact]
    public void Create_EventJson_ParsesEventMetadata()
    {
        const string json = """
            {
              "type": "broadcast",
              "msgtype": "ChangeZone",
              "msg": {
                "zoneID": 129,
                "zoneName": "リムサ・ロミンサ：上甲板層"
              }
            }
            """;

        OverlayWebSocketCommunicationLogEntry entry =
            OverlayWebSocketCommunicationLogEntryFactory.Create("受信", json);

        Assert.Equal("event", entry.Kind);
        Assert.Equal("ChangeZone", entry.Name);
        Assert.Null(entry.SequenceNumber);
    }

    [Fact]
    public void Create_InvalidJson_UsesInvalidJsonKind()
    {
        OverlayWebSocketCommunicationLogEntry entry =
            OverlayWebSocketCommunicationLogEntryFactory.Create("受信", "{ bad-json");

        Assert.Equal("invalid json", entry.Kind);
        Assert.Equal(string.Empty, entry.Name);
        Assert.Null(entry.SequenceNumber);
    }
}
