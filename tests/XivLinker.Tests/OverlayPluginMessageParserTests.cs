using XivLinker.Infrastructure.Overlay.Models;
using XivLinker.Infrastructure.Overlay.Services;

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
                "zoneID": 129,
                "zoneName": "リムサ・ロミンサ：上甲板層"
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
        Assert.Equal((uint)148, snapshot.TerritoryTypeId);
        Assert.Equal(120.5f, snapshot.RawX);
        Assert.Equal(-30.2f, snapshot.RawY);
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
