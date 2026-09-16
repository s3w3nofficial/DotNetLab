using System.Threading.Channels;

namespace DotNetLab;

/// <summary>
/// Serializes language-service work inside the worker. The UI posts immediately;
/// Cancel / compile / SDK stay off this queue so they can overlap the request
/// they abort or the compile they schedule. Not <c>DropOldest</c>: dropping a
/// delta corrupts Roslyn.
/// </summary>
internal sealed class LanguageSession : IDisposable
{
    private readonly Channel<Item> _channel = Channel.CreateUnbounded<Item>(new UnboundedChannelOptions
    {
        SingleReader = true,
        AllowSynchronousContinuations = false,
    });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _reader;

    public LanguageSession()
    {
        _reader = ReadAsync(_cts.Token);
    }

    public Task<WorkerOutputMessage> RunAsync(
        WorkerInputMessage message,
        Func<Task<WorkerOutputMessage>> handle)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(handle);
        if (!IsLanguageService(message))
        {
            return handle();
        }

        var reply = new TaskCompletionSource<WorkerOutputMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_channel.Writer.TryWrite(new Item(handle, reply)))
        {
            reply.TrySetCanceled();
        }

        return reply.Task;
    }

    public static bool IsLanguageService(WorkerInputMessage message)
        => message is
            WorkerInputMessage.OnDidChangeModelContent or
            WorkerInputMessage.OnDidChangeWorkspace or
            WorkerInputMessage.OnCachedCompilationLoaded or
            WorkerInputMessage.ProvideCompletionItems or
            WorkerInputMessage.ResolveCompletionItem or
            WorkerInputMessage.ProvideSemanticTokens or
            WorkerInputMessage.ProvideCodeActions or
            WorkerInputMessage.ProvideHover or
            WorkerInputMessage.ProvideSignatureHelp or
            WorkerInputMessage.GetDiagnostics;

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        Drain();
        _cts.Dispose();
        _ = _reader;
    }

    private void Drain()
    {
        while (_channel.Reader.TryRead(out var leftover))
        {
            leftover.Reply.TrySetCanceled();
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
                    var result = await item.Handle().ConfigureAwait(false);
                    item.Reply.TrySetResult(result);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    item.Reply.TrySetCanceled(cancellationToken);
                    throw;
                }
                catch (Exception ex)
                {
                    item.Reply.TrySetException(ex);
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
        catch (ChannelClosedException)
        {
        }
    }

    private readonly record struct Item(
        Func<Task<WorkerOutputMessage>> Handle,
        TaskCompletionSource<WorkerOutputMessage> Reply);
}
