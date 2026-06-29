using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace XivLinker.Infrastructure.Overlay.Services;

public sealed class OverlayPluginWebSocketService : IOverlayPluginWebSocketService
{
    private readonly TimeSpan requestTimeout;
    private readonly Uri webSocketUri;
    private readonly ILogger<OverlayPluginWebSocketService> logger;
    private long sequenceNumber;

    public OverlayPluginWebSocketService(
        IOptions<OverlayPluginOptions> options,
        ILogger<OverlayPluginWebSocketService>? logger = null)
    {
        OverlayPluginOptions value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        webSocketUri = new Uri(value.WebSocketUri, UriKind.Absolute);
        requestTimeout = TimeSpan.FromSeconds(Math.Max(1, value.RequestTimeoutSeconds));
        this.logger = logger ?? NullLogger<OverlayPluginWebSocketService>.Instance;
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using ClientWebSocket webSocket = await ConnectAsync(cancellationToken);
            return webSocket.State == WebSocketState.Open;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OverlayPlugin WebSocket へ接続できませんでした。Uri={Uri}", webSocketUri);
            return false;
        }
    }

    public async Task<JsonDocument> CallAsync(
        string call,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using ClientWebSocket webSocket = await ConnectAsync(cancellationToken);
        long sequence = Interlocked.Increment(ref sequenceNumber);

        JsonObject payload = new()
        {
            ["type"] = "request",
            ["call"] = call,
            ["rseq"] = sequence,
        };

        if (parameters is not null)
        {
            foreach ((string key, object? value) in parameters)
            {
                payload[key] = value is null ? null : JsonSerializer.SerializeToNode(value);
            }
        }

        byte[] requestBytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        await webSocket.SendAsync(requestBytes, WebSocketMessageType.Text, true, cancellationToken);

        string responseJson = await ReceiveMessageAsync(webSocket, cancellationToken);
        return JsonDocument.Parse(responseJson);
    }

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        using JsonDocument response = await CallAsync("getVersion", cancellationToken: cancellationToken);
        return response.RootElement.TryGetProperty("version", out JsonElement versionElement)
            ? versionElement.GetString()
            : null;
    }

    private async Task<ClientWebSocket> ConnectAsync(CancellationToken cancellationToken)
    {
        ClientWebSocket webSocket = new();
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(requestTimeout);

        try
        {
            logger.LogInformation("OverlayPlugin WebSocket への接続を開始します。Uri={Uri}", webSocketUri);
            await webSocket.ConnectAsync(webSocketUri, timeoutSource.Token);
            logger.LogInformation("OverlayPlugin WebSocket に接続しました。Uri={Uri}", webSocketUri);
            return webSocket;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("OverlayPlugin WebSocket 接続がキャンセルまたはタイムアウトしました。Uri={Uri}", webSocketUri);
            webSocket.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "OverlayPlugin WebSocket 接続に失敗しました。Uri={Uri}", webSocketUri);
            webSocket.Dispose();
            throw;
        }
    }

    private async Task<string> ReceiveMessageAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(requestTimeout);

        byte[] buffer = new byte[8192];
        ArrayBufferWriter<byte> writer = new();

        while (true)
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, timeoutSource.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("OverlayPlugin WebSocket から応答を受信する前に接続が閉じられました。");
            }

            writer.Write(buffer.AsSpan(0, result.Count));
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
