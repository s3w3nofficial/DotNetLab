using AwesomeAssertions;
using DotNetLab.Features.Preferences;
using DotNetLab.Infrastructure.Browser;
using DotNetLab.Infrastructure.Logging;
using DotNetLab.Infrastructure.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace DotNetLab;

[TestClass]
public sealed class WorkerHostSendTests
{
    [TestMethod]
    public async Task SendAsync_AlreadyCancelled_ThrowsWithoutStarting()
    {
        await using var host = new WorkerHost(
            new LabEnvironment(IsDevelopment: false, BaseAddress: "http://localhost/"),
            new LabLogging(),
            new LabSettings(new UnusedJsRuntime()),
            new UnsupportedWorkerTransport(),
            NullLogger<WorkerHost>.Instance);

        var act = async () => await host.SendAsync(
            new WorkerInputMessage.Ping { Id = host.NextMessageId() },
            new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
        host.LastPingResult.Should().BeNull();
    }

    [TestMethod]
    public async Task SendAsync_UnsupportedTransport_UsesInProcessInsteadOfCreateWorker()
    {
        var transport = new SpyInProcessTransport();
        await using var host = new WorkerHost(
            new LabEnvironment(IsDevelopment: false, BaseAddress: "http://localhost/"),
            new LabLogging(),
            new LabSettings(new EmptyPrefsJsRuntime()),
            transport,
            NullLogger<WorkerHost>.Instance);

        var ping = await host.SendAsync(new WorkerInputMessage.Ping { Id = host.NextMessageId() });

        ping.Should().NotBeNull();
        transport.CreateWorkerCalls.Should().Be(0);
        transport.InProcessCalls.Should().Be(1);
        host.LastPingResult.Should().BeNull();
    }

    private sealed class SpyInProcessTransport : IWorkerTransport
    {
        public int CreateWorkerCalls { get; private set; }

        public int InProcessCalls { get; private set; }

        public bool SupportsBackgroundWorker => false;

        public Task EnsureControllerAsync() => Task.CompletedTask;

        public Task EnsureInProcessInteropAsync()
        {
            InProcessCalls++;
            return Task.CompletedTask;
        }

        public IWorkerHandle CreateWorker(string scriptUrl, Action<string> onMessage, Action<string> onError)
        {
            CreateWorkerCalls++;
            throw new InvalidOperationException("Workers are only supported in the browser.");
        }

        public void WorkerReady(IWorkerHandle worker)
        {
        }

        public void PostMessage(IWorkerHandle worker, string message)
        {
        }

        public void PostSideMessage(IWorkerHandle worker, string message)
        {
        }

        public void DisposeWorker(IWorkerHandle worker)
        {
        }

        public void CollectAndDownloadGcDump()
        {
        }
    }

    private sealed class EmptyPrefsJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (typeof(TValue) == typeof(string))
            {
                return (ValueTask<TValue>)(object)new ValueTask<string>("");
            }

            return default;
        }
    }

    private sealed class UnusedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new NotSupportedException();
    }
}
