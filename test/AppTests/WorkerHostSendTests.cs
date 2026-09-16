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

    private sealed class UnusedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new NotSupportedException();
    }
}
