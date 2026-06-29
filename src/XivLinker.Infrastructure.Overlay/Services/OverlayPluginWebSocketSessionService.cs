using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XivLinker.Infrastructure.Overlay.Models;

namespace XivLinker.Infrastructure.Overlay.Services;

public sealed class OverlayPluginWebSocketSessionService : IOverlayPluginWebSocketSessionService, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly ConcurrentDictionary<long, byte> ignoredResponses = new();
    private readonly ConcurrentDictionary<long, string> requestNames = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<string>> pendingRequests = new();
    private readonly TimeSpan requestTimeout;
    private readonly Uri webSocketUri;
    private readonly ILogger<OverlayPluginWebSocketSessionService> logger;
    private ClientWebSocket? webSocket;
    private CancellationTokenSource? receiveLoopCancellationTokenSource;
    private Task? receiveLoopTask;
    private long sequenceNumber;

    public OverlayPluginWebSocketSessionService(
        IOptions<OverlayPluginOptions> options,
        ILogger<OverlayPluginWebSocketSessionService>? logger = null)
    {
        OverlayPluginOptions value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        webSocketUri = new Uri(value.WebSocketUri, UriKind.Absolute);
        requestTimeout = TimeSpan.FromSeconds(Math.Max(1, value.RequestTimeoutSeconds));
        this.logger = logger ?? NullLogger<OverlayPluginWebSocketSessionService>.Instance;
    }

    public event EventHandler? ConnectionStateChanged;

    public event EventHandler<OverlayWebSocketCommunicationLogEntry>? CommunicationLogged;

    public event EventHandler<string>? EventReceived;

    public bool IsStarted => webSocket?.State == WebSocketState.Open;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);

        try
        {
            if (webSocket?.State == WebSocketState.Open)
            {
                return;
            }

            await DisposeCurrentSocketAsync(cancellationToken);

            ClientWebSocket nextWebSocket = new();
            using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(requestTimeout);

            try
            {
                logger.LogInformation("OverlayPlugin WebSocket セッションを開始します。Uri={Uri}", webSocketUri);
                await nextWebSocket.ConnectAsync(webSocketUri, timeoutSource.Token);
                webSocket = nextWebSocket;
                receiveLoopCancellationTokenSource = new CancellationTokenSource();
                receiveLoopTask = Task.Run(() => ReceiveLoopAsync(nextWebSocket, receiveLoopCancellationTokenSource.Token));
                logger.LogInformation("OverlayPlugin WebSocket セッションを開始しました。Uri={Uri}", webSocketUri);
                ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "OverlayPlugin WebSocket セッションの開始に失敗しました。Uri={Uri}", webSocketUri);
                nextWebSocket.Dispose();
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);

        try
        {
            logger.LogInformation("OverlayPlugin WebSocket セッションを停止します。");
            await DisposeCurrentSocketAsync(cancellationToken);
            logger.LogInformation("OverlayPlugin WebSocket セッションを停止しました。");
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string> SendRequestAsync(
        string call,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<string> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        long sequence = 0;

        await gate.WaitAsync(cancellationToken);

        try
        {
            EnsureConnected();

            sequence = CreateSequence();
            pendingRequests[sequence] = completionSource;
            requestNames[sequence] = call;

            JsonObject payload = OverlayPluginRequestFactory.CreateRequestPayload(call, sequence, parameters);
            await SendJsonAsync(payload.ToJsonString(), call, cancellationToken);
        }
        catch
        {
            if (sequence > 0)
            {
                pendingRequests.TryRemove(sequence, out _);
                requestNames.TryRemove(sequence, out _);
            }

            throw;
        }
        finally
        {
            gate.Release();
        }

        Task timeoutTask = Task.Delay(requestTimeout);
        Task cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        Task completedTask = await Task.WhenAny(completionSource.Task, timeoutTask, cancellationTask);

        if (completedTask == completionSource.Task)
        {
            return await completionSource.Task;
        }

        pendingRequests.TryRemove(sequence, out _);
        requestNames.TryRemove(sequence, out _);

        if (completedTask == cancellationTask)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        throw new TimeoutException($"OverlayPlugin WebSocket request timed out after {requestTimeout.TotalSeconds:0} seconds.");
    }

    public async Task SendCommandAsync(
        string call,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        long sequence = 0;

        await gate.WaitAsync(cancellationToken);

        try
        {
            EnsureConnected();

            sequence = CreateSequence();
            ignoredResponses[sequence] = 0;
            requestNames[sequence] = call;

            JsonObject payload = OverlayPluginRequestFactory.CreateRequestPayload(call, sequence, parameters);
            await SendJsonAsync(payload.ToJsonString(), call, cancellationToken);
        }
        catch
        {
            if (sequence > 0)
            {
                ignoredResponses.TryRemove(sequence, out _);
                requestNames.TryRemove(sequence, out _);
            }

            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public Task SubscribeAsync(
        IEnumerable<string> events,
        CancellationToken cancellationToken = default)
    {
        string[] eventArray = events
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return SendCommandAsync(
            "subscribe",
            new Dictionary<string, object?>
            {
                ["events"] = eventArray,
            },
            cancellationToken);
    }

    public void Dispose()
    {
        sendGate.Dispose();
        gate.Dispose();
        receiveLoopCancellationTokenSource?.Cancel();
        receiveLoopCancellationTokenSource?.Dispose();
        webSocket?.Dispose();
    }

    private async Task SendJsonAsync(string json, string? call, CancellationToken cancellationToken)
    {
        EnsureConnected();
        LogCommunication("send", json, call);
        logger.LogDebug("OverlayPlugin へメッセージを送信しました。Call={Call}", call ?? "(unknown)");

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(requestTimeout);

        await sendGate.WaitAsync(timeoutSource.Token);

        try
        {
            await webSocket!.SendAsync(bytes, WebSocketMessageType.Text, true, timeoutSource.Token);
        }
        finally
        {
            sendGate.Release();
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket currentWebSocket, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];

        try
        {
            while (!cancellationToken.IsCancellationRequested && currentWebSocket.State == WebSocketState.Open)
            {
                ArrayBufferWriter<byte> writer = new();
                WebSocketReceiveResult result;

                do
                {
                    result = await currentWebSocket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    writer.Write(buffer.AsSpan(0, result.Count));
                }
                while (!result.EndOfMessage);

                DispatchMessage(Encoding.UTF8.GetString(writer.WrittenSpan));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException exception)
        {
            logger.LogWarning(exception, "OverlayPlugin WebSocket の受信ループが切断されました。");
        }
        finally
        {
            FailPendingRequests("OverlayPlugin WebSocket connection was closed.");

            if (ReferenceEquals(webSocket, currentWebSocket))
            {
                webSocket = null;
                ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void DispatchMessage(string rawJson)
    {
        long? responseSequence = TryReadSequenceNumber(rawJson);
        string? responseName = responseSequence is long sequence && requestNames.TryGetValue(sequence, out string? call)
            ? call
            : null;
        LogCommunication("receive", rawJson, responseName);
        logger.LogDebug("OverlayPlugin からメッセージを受信しました。Call={Call}", responseName ?? "(event)");

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawJson);
            JsonElement root = document.RootElement;

            if (TryReadSequenceNumber(root, out long rseq))
            {
                if (pendingRequests.TryRemove(rseq, out TaskCompletionSource<string>? completionSource))
                {
                    requestNames.TryRemove(rseq, out _);
                    completionSource.TrySetResult(rawJson);
                    return;
                }

                if (ignoredResponses.TryRemove(rseq, out _))
                {
                    requestNames.TryRemove(rseq, out _);
                    return;
                }
            }

            EventReceived?.Invoke(this, rawJson);
        }
        catch (JsonException)
        {
            EventReceived?.Invoke(this, rawJson);
        }
    }

    private async Task DisposeCurrentSocketAsync(CancellationToken cancellationToken)
    {
        receiveLoopCancellationTokenSource?.Cancel();

        if (receiveLoopTask is not null)
        {
            try
            {
                await receiveLoopTask;
            }
            catch
            {
            }
        }

        receiveLoopTask = null;
        receiveLoopCancellationTokenSource?.Dispose();
        receiveLoopCancellationTokenSource = null;

        if (webSocket is null)
        {
            return;
        }

        ClientWebSocket currentWebSocket = webSocket;
        webSocket = null;

        if (currentWebSocket.State == WebSocketState.Open)
        {
            try
            {
                using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(2));
                await currentWebSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "session closed",
                    timeoutSource.Token);
            }
            catch
            {
            }
        }

        currentWebSocket.Dispose();
        FailPendingRequests("OverlayPlugin WebSocket connection was closed.");
    }

    private void FailPendingRequests(string message)
    {
        ignoredResponses.Clear();
        requestNames.Clear();

        foreach ((long sequence, TaskCompletionSource<string> completionSource) in pendingRequests.ToArray())
        {
            if (pendingRequests.TryRemove(sequence, out _))
            {
                completionSource.TrySetException(new InvalidOperationException(message));
            }
        }
    }

    private long CreateSequence()
    {
        return Interlocked.Increment(ref sequenceNumber);
    }

    private void EnsureConnected()
    {
        if (webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("OverlayPlugin WebSocket is not connected.");
        }
    }

    private void LogCommunication(string direction, string rawJson, string? fallbackName)
    {
        OverlayWebSocketCommunicationLogEntry entry = OverlayWebSocketCommunicationLogEntryFactory.Create(
            direction,
            rawJson,
            fallbackName);
        CommunicationLogged?.Invoke(this, entry);
    }

    private static long? TryReadSequenceNumber(string rawJson)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(rawJson);
            return TryReadSequenceNumber(document.RootElement, out long sequenceNumber)
                ? sequenceNumber
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadSequenceNumber(JsonElement root, out long sequenceNumber)
    {
        if (root.TryGetProperty("rseq", out JsonElement property))
        {
            return TryConvertSequenceNumber(property, out sequenceNumber);
        }

        foreach (JsonProperty candidate in root.EnumerateObject())
        {
            if (string.Equals(candidate.Name, "rseq", StringComparison.OrdinalIgnoreCase))
            {
                return TryConvertSequenceNumber(candidate.Value, out sequenceNumber);
            }
        }

        sequenceNumber = 0;
        return false;
    }

    private static bool TryConvertSequenceNumber(JsonElement property, out long sequenceNumber)
    {
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out sequenceNumber))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String
            && long.TryParse(property.GetString(), out sequenceNumber))
        {
            return true;
        }

        sequenceNumber = 0;
        return false;
    }
}
