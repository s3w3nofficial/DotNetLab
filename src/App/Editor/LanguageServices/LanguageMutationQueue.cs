using System.Threading.Channels;

namespace DotNetLab.Editor.LanguageServices;

/// <summary>
/// Serializes incremental language-service mutations (document deltas, workspace
/// snapshots). Queries and Cancel stay on <see cref="Infrastructure.Worker.WorkerHost"/>
/// and must not go through this queue. Not <c>DropOldest</c>: dropping a delta
/// corrupts Roslyn.
/// </summary>
internal sealed class LanguageMutationQueue : IAsyncDisposable
{
    private readonly Channel<Item> _channel = Channel.CreateUnbounded<Item>(new UnboundedChannelOptions
    {
        SingleReader = true,
        AllowSynchronousContinuations = false,
    });
    private readonly ILogger _logger;
    private CancellationTokenSource _cts = new();
    private Task _reader;

    public LanguageMutationQueue(ILogger logger)
    {
        _logger = logger;
        _reader = ReadAsync(_cts.Token);
    }

    /// <summary>
    /// Writes work and returns a task that completes when it has been applied.
    /// The write itself does not wait for apply (keystrokes must not sit behind
    /// completion/hover).
    /// </summary>
    public Task EnqueueAsync(Func<CancellationToken, Task> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        var item = new Item(apply, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        if (!_channel.Writer.TryWrite(item))
        {
            item.Applied.TrySetCanceled();
        }

        return item.Applied.Task;
    }

    public Task EnqueueBarrierAsync() => EnqueueAsync(static _ => Task.CompletedTask);

    public void Cancel()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async Task RestartAsync()
    {
        try
        {
            await _reader.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        Drain();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _reader = ReadAsync(_cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        Cancel();
        try
        {
            await _reader.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        Drain();
        _cts.Dispose();
    }

    private void Drain()
    {
        while (_channel.Reader.TryRead(out var leftover))
        {
            leftover.Applied.TrySetCanceled();
        }
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await item.ApplyAsync(cancellationToken).ConfigureAwait(false);
                    item.Applied.TrySetResult();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    item.Applied.TrySetCanceled(cancellationToken);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Language mutation failed");
                    item.Applied.TrySetException(ex);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private readonly record struct Item(Func<CancellationToken, Task> ApplyAsync, TaskCompletionSource Applied);
}
