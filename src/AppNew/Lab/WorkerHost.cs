namespace DotNetLab.Lab;

/// <summary>
/// Owns the compiler/worker <see cref="IServiceProvider"/> created by
/// <see cref="WorkerServices"/>. This is a separate container from the Blazor UI
/// host — same split as <c>src/App</c> uses via <c>WorkerController</c>.
/// </summary>
public sealed class WorkerHost(IServiceProvider services)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _messageId;

    public WorkerInputMessage.IExecutor Executor
        => services.GetRequiredService<WorkerInputMessage.IExecutor>();

    public int NextMessageId() => Interlocked.Increment(ref _messageId);

    public async Task<T> SendAsync<T>(IWorkerInputMessage<T> message)
    {
        await _gate.WaitAsync();
        try
        {
            return await message.HandleAsync(Executor);
        }
        finally
        {
            _gate.Release();
        }
    }
}
