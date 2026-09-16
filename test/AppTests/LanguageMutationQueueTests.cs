using AwesomeAssertions;
using DotNetLab.Editor.LanguageServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetLab;

[TestClass]
public sealed class LanguageMutationQueueTests
{
    [TestMethod]
    public async Task Enqueue_ReturnsBeforeApplyCompletes()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var queue = new LanguageMutationQueue(NullLogger.Instance);

        var applied = queue.EnqueueAsync(async ct =>
        {
            started.SetResult();
            await release.Task.WaitAsync(ct);
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        applied.IsCompleted.Should().BeFalse();

        release.SetResult();
        await applied.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task Mutations_ApplyInOrder()
    {
        var order = new List<int>();
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var queue = new LanguageMutationQueue(NullLogger.Instance);

        var first = queue.EnqueueAsync(async _ =>
        {
            await firstGate.Task;
            order.Add(1);
        });
        var second = queue.EnqueueAsync(_ =>
        {
            order.Add(2);
            return Task.CompletedTask;
        });

        first.IsCompleted.Should().BeFalse();
        second.IsCompleted.Should().BeFalse();
        order.Should().BeEmpty();

        firstGate.SetResult();
        await second.WaitAsync(TimeSpan.FromSeconds(1));
        order.Should().Equal(1, 2);
        first.IsCompletedSuccessfully.Should().BeTrue();
    }

    [TestMethod]
    public async Task Barrier_WaitsForPriorMutations()
    {
        var applied = false;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var queue = new LanguageMutationQueue(NullLogger.Instance);

        _ = queue.EnqueueAsync(async _ =>
        {
            await gate.Task;
            applied = true;
        });
        var barrier = queue.EnqueueBarrierAsync();

        barrier.IsCompleted.Should().BeFalse();
        applied.Should().BeFalse();

        gate.SetResult();
        await barrier.WaitAsync(TimeSpan.FromSeconds(1));
        applied.Should().BeTrue();
    }

    [TestMethod]
    public async Task Reset_DrainsLeftoverWithoutApplying()
    {
        var applied = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var queue = new LanguageMutationQueue(NullLogger.Instance);

        var first = queue.EnqueueAsync(async ct =>
        {
            await gate.Task.WaitAsync(ct);
            applied++;
        });
        var leftover = queue.EnqueueAsync(_ =>
        {
            applied++;
            return Task.CompletedTask;
        });

        queue.Cancel();
        await queue.RestartAsync().WaitAsync(TimeSpan.FromSeconds(1));

        leftover.IsCanceled.Should().BeTrue();
        first.IsCanceled.Should().BeTrue();
        applied.Should().Be(0);

        await queue.EnqueueAsync(_ =>
        {
            applied++;
            return Task.CompletedTask;
        }).WaitAsync(TimeSpan.FromSeconds(1));
        applied.Should().Be(1);
    }
}
