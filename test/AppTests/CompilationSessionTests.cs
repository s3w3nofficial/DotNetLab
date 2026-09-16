using System.Text.Json;
using AwesomeAssertions;
using DotNetLab.Features.Compilation;
using DotNetLab.Features.Compiler;
using DotNetLab.Features.Outputs;
using DotNetLab.Features.Preferences;
using DotNetLab.Features.Workspace;
using DotNetLab.Infrastructure.Browser;
using DotNetLab.Infrastructure.Logging;
using DotNetLab.Infrastructure.Persistence;
using DotNetLab.Infrastructure.Worker;
using DotNetLab.Lab;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace DotNetLab;

[TestClass]
public sealed class CompilationSessionTests
{
    [TestMethod]
    public async Task QuietCompile_DoesNotOverlapAnotherCompile()
    {
        using var context = ImmediateSynchronizationContext.Install();
        var transport = new DelayedCompileTransport();
        await using var worker = CreateWorker(transport);
        var compilation = new Store<CompilationState>(new CompilationState());
        var dispatcher = new RecordingDispatcher(compilation);
        var session = CreateSession(worker, compilation, dispatcher);

        var first = session.CompileAsync(storeInCache: false, updateDisplayedOutput: false);
        await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var second = session.CompileAsync(storeInCache: false, updateDisplayedOutput: false);
        await second.WaitAsync(TimeSpan.FromSeconds(1));
        transport.CompileCount.Should().Be(1);

        transport.Release.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(2));
        transport.CompileCount.Should().Be(1);

        dispatcher.Actions.OfType<SetRunningAction>().Should().BeEmpty();
    }

    [TestMethod]
    public async Task BusyCompile_SetsRunningWhileInFlight()
    {
        using var context = ImmediateSynchronizationContext.Install();
        var transport = new DelayedCompileTransport();
        await using var worker = CreateWorker(transport);
        var compilation = new Store<CompilationState>(new CompilationState());
        var dispatcher = new RecordingDispatcher(compilation);
        var session = CreateSession(worker, compilation, dispatcher);

        var compile = session.CompileAsync(storeInCache: true, updateDisplayedOutput: true);
        await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        compilation.Value.Running.Should().BeTrue();

        transport.Release.SetResult();
        await compile.WaitAsync(TimeSpan.FromSeconds(2));
        compilation.Value.Running.Should().BeFalse();
    }

    private static WorkerHost CreateWorker(IWorkerTransport transport)
        => new(
            new LabEnvironment(IsDevelopment: false, BaseAddress: "http://localhost/"),
            new LabLogging(),
            new LabSettings(new EmptyPrefsJsRuntime()),
            transport,
            NullLogger<WorkerHost>.Instance);

    private static CompilationSession CreateSession(
        WorkerHost worker,
        IState<CompilationState> compilation,
        IDispatcher dispatcher)
    {
        var host = new FakeWorkspace();
        return new CompilationSession(
            host,
            worker,
            new TemplateCache(),
            new InputOutputCache(new HttpClient { BaseAddress = new Uri("http://localhost/") }, NullLogger<InputOutputCache>.Instance),
            new Store<CompilerState>(new CompilerState()),
            new Store<PreferencesState>(new PreferencesState { EnableCaching = false, LanguageServices = false }),
            compilation,
            dispatcher,
            NullLogger.Instance);
    }

    private sealed class DelayedCompileTransport : IWorkerTransport
    {
        private Action<string>? _onMessage;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CompileCount { get; private set; }

        public Task EnsureControllerAsync() => Task.CompletedTask;

        public Task EnsureInProcessInteropAsync() => Task.CompletedTask;

        public IWorkerHandle CreateWorker(string scriptUrl, Action<string> onMessage, Action<string> onError)
        {
            _ = scriptUrl;
            _ = onError;
            _onMessage = onMessage;
            onMessage(Serialize(new WorkerOutputMessage.Ready
            {
                Id = WorkerOutputMessage.BroadcastId,
                InputType = WorkerOutputMessage.NoInputType,
            }));
            return new Handle();
        }

        public void WorkerReady(IWorkerHandle worker)
        {
        }

        public void PostMessage(IWorkerHandle worker, string message)
        {
            var incoming = JsonSerializer.Deserialize(message, WorkerJsonContext.Default.WorkerInputMessage);
            if (incoming is not WorkerInputMessage.Compile compile)
            {
                return;
            }

            CompileCount++;
            Started.TrySetResult();
            _ = ReplyAsync(compile.Id);
        }

        public void PostSideMessage(IWorkerHandle worker, string message)
        {
        }

        public void DisposeWorker(IWorkerHandle worker) => worker.Dispose();

        public void CollectAndDownloadGcDump()
        {
        }

        private async Task ReplyAsync(int id)
        {
            await Release.Task;
            _onMessage!(Serialize(new WorkerOutputMessage.Success(CompiledAssembly.Fail("ok"))
            {
                Id = id,
                InputType = nameof(WorkerInputMessage.Compile),
            }));
        }

        private static string Serialize(WorkerOutputMessage message)
            => JsonSerializer.Serialize(message, WorkerJsonContext.Default.WorkerOutputMessage);

        private sealed class Handle : IWorkerHandle
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class FakeWorkspace : ICompilationWorkspace, IOutputLoadHost, IOutputWorkspace
    {
        public FakeWorkspace()
        {
            OutputCache = new OutputLoadCache(this);
            Tabs = new OutputTabLayout(this);
        }

        public OutputLoadCache OutputCache { get; }

        public OutputTabLayout Tabs { get; }

        public CompilationInput CreateCompilationInput()
            => new(new([new() { FileName = "Program.cs", Text = "class C;" }]));

        public SavedState CaptureSavedState() => SavedState.CSharp;

        public void Notify()
        {
        }

        public Task PersistUrlAsync(bool snapshot = false) => Task.CompletedTask;

        public Task RefreshLanguageServicesAfterCompileAsync() => Task.CompletedTask;

        public Task RefreshLanguageServicesAfterCachedCompileAsync(CompiledAssembly output)
            => Task.CompletedTask;

        public string ActiveSource => "Program.cs";

        public string ActiveOutput { get; set; } = "cs";

        public bool Running => false;

        public CompiledAssembly? Compiled => null;

        public CompilationInput? LastInput => null;

        public bool StoreInCache => false;

        public int CompileGeneration => 0;

        public bool IsCurrentCompile(int generation) => true;

        public string OutputLabel(string tab) => tab;

        public ValueTask<CompiledFileLazyResult> LoadFromWorkerAsync(string? file, string tab)
            => ValueTask.FromResult(new CompiledFileLazyResult { Text = "" });

        public void StoreCompiledOutput(CompiledAssembly compiled)
        {
        }
    }

    private sealed class Store<T>(T value) : IState<T>
    {
        public T Value { get; set; } = value;

        public event EventHandler StateChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class RecordingDispatcher(Store<CompilationState> compilation) : IDispatcher
    {
        public List<object> Actions { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs> ActionDispatched
        {
            add { }
            remove { }
        }

        public void Dispatch(object action)
        {
            Actions.Add(action);
            if (action is SetRunningAction running)
            {
                compilation.Value = compilation.Value with { Running = running.Value };
            }
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

    private sealed class ImmediateSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly SynchronizationContext? _previous = Current;

        public static ImmediateSynchronizationContext Install()
        {
            var context = new ImmediateSynchronizationContext();
            SetSynchronizationContext(context);
            return context;
        }

        public override void Post(SendOrPostCallback d, object? state) => d(state);

        public override void Send(SendOrPostCallback d, object? state) => d(state);

        public void Dispose() => SetSynchronizationContext(_previous);
    }
}
