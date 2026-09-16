using AwesomeAssertions;
using DotNetLab.Lab;

namespace DotNetLab;

[TestClass]
public sealed class LanguageSessionTests
{
    [TestMethod]
    public async Task LanguageMessages_RunInArrivalOrder()
    {
        var order = new List<int>();
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = new LanguageSession();

        var first = session.RunAsync(Hover(1), async () =>
        {
            await firstGate.Task;
            order.Add(1);
            return Empty(1);
        });
        var second = session.RunAsync(Hover(2), () =>
        {
            order.Add(2);
            return Task.FromResult(Empty(2));
        });

        first.IsCompleted.Should().BeFalse();
        second.IsCompleted.Should().BeFalse();
        order.Should().BeEmpty();

        firstGate.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
        order.Should().Equal(1, 2);
    }

    [TestMethod]
    public async Task Cancel_DoesNotWaitForLanguageWork()
    {
        var languageStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var languageRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelRan = false;
        using var session = new LanguageSession();

        var language = session.RunAsync(Hover(1), async () =>
        {
            languageStarted.SetResult();
            await languageRelease.Task;
            return Empty(1);
        });
        await languageStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var cancel = session.RunAsync(new WorkerInputMessage.Cancel(1) { Id = 2 }, () =>
        {
            cancelRan = true;
            return Task.FromResult(Empty(2));
        });
        await cancel.WaitAsync(TimeSpan.FromSeconds(2));
        cancelRan.Should().BeTrue();
        language.IsCompleted.Should().BeFalse();

        languageRelease.SetResult();
        await language.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task Ping_DoesNotWaitForLanguageWork()
    {
        var languageStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var languageRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = new LanguageSession();

        var language = session.RunAsync(Hover(1), async () =>
        {
            languageStarted.SetResult();
            await languageRelease.Task;
            return Empty(1);
        });
        await languageStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var ping = session.RunAsync(new WorkerInputMessage.Ping { Id = 2 }, () => Task.FromResult(Empty(2)));
        await ping.WaitAsync(TimeSpan.FromSeconds(2));
        language.IsCompleted.Should().BeFalse();

        languageRelease.SetResult();
        await language.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsLanguageService_ClassifiesMutationsAndQueries()
    {
        LanguageSession.IsLanguageService(Hover(1)).Should().BeTrue();
        LanguageSession.IsLanguageService(new WorkerInputMessage.GetDiagnostics("u") { Id = 2 }).Should().BeTrue();
        LanguageSession.IsLanguageService(new WorkerInputMessage.OnDidChangeModelContent(
            "u",
            new BlazorMonaco.Editor.ModelContentChangedEvent())
        { Id = 3 }).Should().BeTrue();
        LanguageSession.IsLanguageService(new WorkerInputMessage.Cancel(1) { Id = 4 }).Should().BeFalse();
        LanguageSession.IsLanguageService(new WorkerInputMessage.Ping { Id = 5 }).Should().BeFalse();
        LanguageSession.IsLanguageService(new WorkerInputMessage.Compile(
            new CompilationInput(new([new InputCode { FileName = "Program.cs", Text = "" }])),
            LanguageServicesEnabled: false)
        { Id = 6 }).Should().BeFalse();
    }

    private static WorkerInputMessage.ProvideHover Hover(int id)
        => new("u", "{}") { Id = id };

    private static WorkerOutputMessage Empty(int id)
        => new WorkerOutputMessage.Empty { Id = id, InputType = "test" };
}
