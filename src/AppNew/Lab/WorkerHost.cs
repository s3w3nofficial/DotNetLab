namespace DotNetLab.Lab;

/// <summary>
/// Owns the compiler/worker <see cref="IServiceProvider"/> created by
/// <see cref="WorkerServices"/>. This is a separate container from the Blazor UI
/// host — same split as <c>src/App</c> uses via <c>WorkerController</c>.
/// </summary>
public sealed class WorkerHost
{
    private readonly string _baseUrl;
    private readonly Func<LogLevel> _logLevel;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IServiceProvider _services;
    private int _messageId;

    public WorkerHost(string baseUrl, Func<LogLevel> logLevel)
    {
        _baseUrl = baseUrl;
        _logLevel = logLevel;
        _services = CreateServices();
    }

    public WorkerInputMessage.IExecutor Executor
        => _services.GetRequiredService<WorkerInputMessage.IExecutor>();

    public int NextMessageId() => Interlocked.Increment(ref _messageId);

    public async Task RecreateAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await DisposeServicesAsync(_services);
            _services = CreateServices();
            _messageId = 0;
        }
        finally
        {
            _gate.Release();
        }
    }

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

    private IServiceProvider CreateServices()
        => WorkerServices.Create(_baseUrl, _logLevel());

    private static async ValueTask DisposeServicesAsync(IServiceProvider services)
    {
        switch (services)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
