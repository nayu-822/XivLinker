using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace XivLinker.App.Logging;

public sealed class XivLinkerFileLogWriter : IDisposable
{
    private readonly ConcurrentQueue<WriteRequest> queue = new();
    private readonly SemaphoreSlim signal = new(0);
    private readonly CancellationTokenSource disposeCancellationTokenSource = new();
    private readonly Task backgroundTask;
    private readonly FileLogOptions options;
    private StreamWriter? writer;
    private DateOnly? currentDate;
    private bool disposed;

    public XivLinkerFileLogWriter(FileLogOptions options)
    {
        this.options = options;
        backgroundTask = Task.Run(ProcessQueueAsync);
    }

    public void Enqueue(DateTime timestamp, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (disposed)
        {
            return;
        }

        queue.Enqueue(new WriteRequest(timestamp, text));
        signal.Release();
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (disposed)
        {
            return;
        }

        TaskCompletionSource<bool> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Enqueue(new WriteRequest(DateTime.Now, string.Empty, completionSource, FlushOnly: true));
        signal.Release();

        using CancellationTokenRegistration _ = cancellationToken.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(),
            completionSource);
        await completionSource.Task;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        FlushAsync().GetAwaiter().GetResult();
        disposeCancellationTokenSource.Cancel();
        signal.Release();

        try
        {
            backgroundTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        writer?.Dispose();
        signal.Dispose();
        disposeCancellationTokenSource.Dispose();
    }

    private async Task ProcessQueueAsync()
    {
        while (!disposeCancellationTokenSource.IsCancellationRequested || !queue.IsEmpty)
        {
            try
            {
                await signal.WaitAsync(disposeCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                if (queue.IsEmpty)
                {
                    break;
                }
            }

            while (queue.TryDequeue(out WriteRequest? request))
            {
                if (request.FlushOnly)
                {
                    await FlushWriterCoreAsync();
                    request.CompletionSource?.TrySetResult(true);
                    continue;
                }

                await WriteCoreAsync(request.Timestamp, request.Text);
            }
        }

        await FlushWriterCoreAsync();
    }

    private async Task WriteCoreAsync(DateTime timestamp, string text)
    {
        StreamWriter currentWriter = EnsureWriter(timestamp);
        await currentWriter.WriteLineAsync(text);
    }

    private async Task FlushWriterCoreAsync()
    {
        if (writer is not null)
        {
            await writer.FlushAsync();
        }
    }

    private StreamWriter EnsureWriter(DateTime timestamp)
    {
        string logsPath = options.LogsPath;
        Directory.CreateDirectory(logsPath);

        DateOnly requestedDate = DateOnly.FromDateTime(timestamp);
        if (writer is not null && currentDate == requestedDate)
        {
            return writer;
        }

        writer?.Dispose();

        string filePath = Path.Combine(logsPath, $"xivlinker-{timestamp:yyyyMMdd}.log");
        writer = new StreamWriter(
            new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        currentDate = requestedDate;
        return writer;
    }

    private sealed record WriteRequest(
        DateTime Timestamp,
        string Text,
        TaskCompletionSource<bool>? CompletionSource = null,
        bool FlushOnly = false);
}
