using XivLinker.Infrastructure.Overlay.Models;
using XivLinker.Infrastructure.Overlay.Services;
using System.Text.Json.Nodes;

namespace XivLinker.Tests;

public sealed class OverlayPluginMessageParserTests
{
    [Fact]
    public void TryParseEventMessage_ParsesChangeZoneBroadcast()
    {
        const string json = """
            {
              "type": "broadcast",
              "msgtype": "ChangeZone",
              "msg": {
                "zoneId": 129,
                "ZoneName": "リムサ・ロミンサ：上甲板層"
              }
            }
            """;

        bool parsed = OverlayPluginMessageParser.TryParseEventMessage(json, out OverlayPluginEventMessage? message);
        bool zoneParsed = OverlayPluginMessageParser.TryParseChangeZone(message!, out uint territoryTypeId, out string zoneName);

        Assert.True(parsed);
        Assert.True(zoneParsed);
        Assert.Equal((uint)129, territoryTypeId);
        Assert.Equal("リムサ・ロミンサ：上甲板層", zoneName);
    }

    [Fact]
    public void OverlayPluginRequestFactory_CreateRequestPayload_BuildsSubscribePayload()
    {
        JsonObject payload = OverlayPluginRequestFactory.CreateRequestPayload(
            "subscribe",
            7,
            new Dictionary<string, object?>
            {
                ["events"] = new[] { "ChangeZone", "ChangePrimaryPlayer" },
            });

        Assert.Equal("request", payload["type"]?.GetValue<string>());
        Assert.Equal("subscribe", payload["call"]?.GetValue<string>());
        Assert.Equal(7L, payload["rseq"]?.GetValue<long>());
        Assert.Equal(
            """["ChangeZone","ChangePrimaryPlayer"]""",
            payload["events"]?.ToJsonString());
    }

    [Fact]
    public void TryParseEventMessage_ParsesPrimaryPlayerBroadcast()
    {
        const string json = """
            {
              "type": "broadcast",
              "msgtype": "ChangePrimaryPlayer",
              "msg": {
                "charName": "Example Crafter"
              }
            }
            """;

        bool parsed = OverlayPluginMessageParser.TryParseEventMessage(json, out OverlayPluginEventMessage? message);
        bool playerParsed = OverlayPluginMessageParser.TryParsePrimaryPlayer(message!, out string playerName);

        Assert.True(parsed);
        Assert.True(playerParsed);
        Assert.Equal("Example Crafter", playerName);
    }

    [Fact]
    public void TryParseEventMessage_ParsesChangeZoneFromMsgTypePayload()
    {
        const string json = """
            {
              "type": "broadcast",
              "msg": {
                "type": "ChangeZone",
                "zoneID": 123,
                "zoneName": "テストエリア"
              }
            }
            """;

        bool parsed = OverlayPluginMessageParser.TryParseEventMessage(json, out OverlayPluginEventMessage? message);
        bool zoneParsed = OverlayPluginMessageParser.TryParseChangeZone(message!, out uint territoryTypeId, out string zoneName);

        Assert.True(parsed);
        Assert.Equal("ChangeZone", message!.MessageType);
        Assert.True(zoneParsed);
        Assert.Equal((uint)123, territoryTypeId);
        Assert.Equal("テストエリア", zoneName);
    }

    [Fact]
    public void TryParseEventMessage_ParsesPrimaryPlayerFromMsgTypePayload()
    {
        const string json = """
            {
              "type": "broadcast",
              "msg": {
                "type": "ChangePrimaryPlayer",
                "charName": "Test Player"
              }
            }
            """;

        bool parsed = OverlayPluginMessageParser.TryParseEventMessage(json, out OverlayPluginEventMessage? message);
        bool playerParsed = OverlayPluginMessageParser.TryParsePrimaryPlayer(message!, out string playerName);

        Assert.True(parsed);
        Assert.Equal("ChangePrimaryPlayer", message!.MessageType);
        Assert.True(playerParsed);
        Assert.Equal("Test Player", playerName);
    }

    [Fact]
    public void TryParseEventMessage_ParsesEventPayloadShape()
    {
        const string json = """
            {
              "type": "event",
              "event": "ChangeZone",
              "payload": {
                "territoryTypeId": 339,
                "zoneName": "黒衣森：中央森林"
              }
            }
            """;

        bool parsed = OverlayPluginMessageParser.TryParseEventMessage(json, out OverlayPluginEventMessage? message);
        bool zoneParsed = OverlayPluginMessageParser.TryParseChangeZone(message!, out uint territoryTypeId, out string zoneName);

        Assert.True(parsed);
        Assert.True(zoneParsed);
        Assert.Equal((uint)339, territoryTypeId);
        Assert.Equal("黒衣森：中央森林", zoneName);
    }

    [Fact]
    public void TryParseEventMessage_ParsesRootPayloadShape()
    {
        const string json = """
            {
              "type": "event",
              "name": "ChangePrimaryPlayer",
              "charName": "Example Crafter"
            }
            """;

        bool parsed = OverlayPluginMessageParser.TryParseEventMessage(json, out OverlayPluginEventMessage? message);
        bool playerParsed = OverlayPluginMessageParser.TryParsePrimaryPlayer(message!, out string playerName);

        Assert.True(parsed);
        Assert.True(playerParsed);
        Assert.Equal("Example Crafter", playerName);
    }

    [Fact]
    public void TryParseChangeZone_ParsesRootChangeZonePayload()
    {
        const string json = """
            {"type":"ChangeZone","zoneID":979,"zoneName":"Empyreum"}
            """;

        Assert.True(OverlayPluginMessageParser.TryParseEventMessage(json, out OverlayPluginEventMessage? message));
        Assert.NotNull(message);
        Assert.Equal("ChangeZone", message!.MessageType);

        Assert.True(OverlayPluginMessageParser.TryParseChangeZone(
            message,
            out uint territoryTypeId,
            out string zoneName));

        Assert.Equal<uint>(979, territoryTypeId);
        Assert.Equal("Empyreum", zoneName);
    }

    [Fact]
    public void TryParseEventMessage_UsesRootAsPayload_ForRootChangeZone()
    {
        const string json = """
            {"type":"ChangeZone","zoneID":979,"zoneName":"Empyreum"}
            """;

        Assert.True(OverlayPluginMessageParser.TryParseEventMessage(json, out OverlayPluginEventMessage? message));
        Assert.NotNull(message);

        Assert.Equal("ChangeZone", message!.MessageType);
        Assert.Equal(979u, message.Payload.GetProperty("zoneID").GetUInt32());
        Assert.Equal("Empyreum", message.Payload.GetProperty("zoneName").GetString());
    }

    [Fact]
    public void TryParseEventMessage_ResponseWithSequence_IsNotEvent()
    {
        const string json = """
            {
              "rseq": 1,
              "combatants": []
            }
            """;

        bool parsed = OverlayPluginMessageParser.TryParseEventMessage(json, out OverlayPluginEventMessage? message);

        Assert.False(parsed);
        Assert.Null(message);
    }

    [Fact]
    public void TryParseCurrentPlayerSnapshot_SelectsNamedCombatant()
    {
        const string json = """
            {
              "combatants": [
                {
                  "Name": "Other Player",
                  "Job": 19,
                  "Level": 100,
                  "PosX": 11.2,
                  "PosY": 22.3,
                  "PosZ": 1.5
                },
                {
                  "Name": "Example Crafter",
                  "Job": 11,
                  "Level": 90,
                  "CurrentZoneID": 148,
                  "PosX": 120.5,
                  "PosY": -30.2,
                  "PosZ": 7.8
                }
              ]
            }
            """;

        OverlayCurrentPlayerSnapshot? snapshot = OverlayPluginMessageParser.TryParseCurrentPlayerSnapshot(json, "Example Crafter");

        Assert.NotNull(snapshot);
        Assert.Equal("Example Crafter", snapshot!.PlayerName);
        Assert.Equal((uint)11, snapshot.ClassJobId);
        Assert.Equal(90, snapshot.Level);
        Assert.Equal((uint)148, snapshot.CombatantTerritoryTypeId);
        Assert.Null(snapshot.CombatantMapId);
        Assert.Equal(120.5f, snapshot.RawX);
        Assert.Equal(-30.2f, snapshot.RawY);
    }

    [Fact]
    public void TryParseCurrentPlayerSnapshot_DoesNotTreatCurrentMapIdAsTerritoryTypeId()
    {
        const string json = """
            {
              "combatants": [
                {
                  "Name": "Example Crafter",
                  "Job": 11,
                  "Level": 90,
                  "CurrentMapID": 584,
                  "PosX": 120.5,
                  "PosY": -30.2,
                  "PosZ": 7.8
                }
              ]
            }
            """;

        OverlayCurrentPlayerSnapshot? snapshot = OverlayPluginMessageParser.TryParseCurrentPlayerSnapshot(json, "Example Crafter");

        Assert.NotNull(snapshot);
        Assert.Null(snapshot!.CombatantTerritoryTypeId);
        Assert.Equal((uint)584, snapshot.CombatantMapId);
    }

    [Fact]
    public void TryParseCurrentPlayerSnapshot_AllowsJobWithoutCoordinatesOrTerritory()
    {
        const string json = """
            {
              "combatants": [
                {
                  "Name": "Example Crafter",
                  "Job": 11,
                  "Level": 90
                }
              ]
            }
            """;

        OverlayCurrentPlayerSnapshot? snapshot = OverlayPluginMessageParser.TryParseCurrentPlayerSnapshot(json, "Example Crafter");

        Assert.NotNull(snapshot);
        Assert.Equal((uint)11, snapshot!.ClassJobId);
        Assert.Equal(90, snapshot.Level);
        Assert.Null(snapshot.CombatantTerritoryTypeId);
        Assert.Null(snapshot.CombatantMapId);
        Assert.Equal(0, snapshot.RawX);
        Assert.Equal(0, snapshot.RawY);
    }

    [Fact]
    public void TryParseEventMessage_WithUnexpectedJson_DoesNotThrow()
    {
        const string json = "{ not-valid-json";

        bool parsed = OverlayPluginMessageParser.TryParseEventMessage(json, out OverlayPluginEventMessage? message);
        OverlayCurrentPlayerSnapshot? snapshot = OverlayPluginMessageParser.TryParseCurrentPlayerSnapshot(json);

        Assert.False(parsed);
        Assert.Null(message);
        Assert.Null(snapshot);
    }
}
