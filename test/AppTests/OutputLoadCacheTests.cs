using AwesomeAssertions;
using DotNetLab.Features.Compiler;
using DotNetLab.Features.Outputs;
using DotNetLab.Lab;

namespace DotNetLab;

[TestClass]
public sealed class OutputLoadCacheTests
{
    [TestMethod]
    public void GetOutput_AsksToCompileWhenThereIsNoAssembly()
    {
        var (cache, _) = Create();
        cache.GetOutput("cs").Should().Be("(press Compile to load this)");
        cache.IsEmpty.Should().BeTrue();
    }

    [TestMethod]
    public void GetOutput_ShowsCompilingPlaceholderWhileRunning()
    {
        var (cache, host) = Create();
        host.Running = true;
        cache.GetOutput("cs").Should().Be("Compiling…");
    }

    [TestMethod]
    public void GetOutput_UsesFailOutput()
    {
        var (cache, host) = Create();
        host.Compiled = CompiledAssembly.Fail("boom");
        cache.GetOutput("cs").Should().Be("boom");
    }

    [TestMethod]
    public void GetOutput_CachesEagerTextUntilCleared()
    {
        var (cache, host) = Create();
        host.Compiled = AssemblyWithEager("cs", "class C;", "csharp");
        cache.GetOutput("cs").Should().Be("class C;");
        cache.OutputLanguage("cs").Should().Be("csharp");
        cache.IsEmpty.Should().BeFalse();

        host.Compiled = null;
        cache.GetOutput("cs").Should().Be("class C;");

        cache.Clear();
        cache.GetOutput("cs").Should().Be("(press Compile to load this)");
        cache.IsEmpty.Should().BeTrue();
    }

    [TestMethod]
    public void GetOutput_MissingTabUsesLabel()
    {
        var (cache, host) = Create();
        host.Compiled = AssemblyWithEager("il", ".class", "il");
        cache.GetOutput("tree").Should().Be("(no Tree output for this file)");
    }

    [TestMethod]
    public void DisplayType_SwitchesToErrorListWhenOutputIsEmpty()
    {
        var (cache, host) = Create();
        host.ActiveOutput = "cs";
        host.Compiled = AssemblyWithEager("cs", "", "csharp", errors: 1);
        cache.SetTemporaryErrorList(true);
        cache.DisplayType.Should().Be(LabCatalog.ErrorsOutputType);
        cache.DismissTemporaryErrorList().Should().BeTrue();
        cache.DisplayType.Should().Be("cs");
    }

    [TestMethod]
    public async Task EnsureOutputLoadedAsync_DropsStaleGeneration()
    {
        var (cache, host) = Create();
        host.LastInput = new CompilationInput(new(ImmutableArray<InputCode>.Empty));
        host.CompileGeneration = 1;
        host.Compiled = new CompiledAssembly(
            Files: ImmutableSortedDictionary<string, CompiledFile>.Empty.Add(
                "Program.cs",
                new CompiledFile(
                [
                    new CompiledFileOutput
                    {
                        Type = "cs",
                        Label = "C#",
                        LazyText = async () =>
                        {
                            host.CompileGeneration++;
                            await Task.Yield();
                            return "late";
                        },
                    },
                ])),
            GlobalOutputs: [],
            NumWarnings: 0,
            NumErrors: 0,
            Diagnostics: [],
            BaseDirectory: "/");

        await cache.EnsureOutputLoadedAsync("cs");
        cache.IsEmpty.Should().BeTrue();
        host.StoredCount.Should().Be(0);
    }

    private static (OutputLoadCache Cache, FakeOutputLoadHost Host) Create()
    {
        var host = new FakeOutputLoadHost();
        return (new OutputLoadCache(host), host);
    }

    private static CompiledAssembly AssemblyWithEager(string type, string text, string language, int errors = 0)
        => new(
            Files: ImmutableSortedDictionary<string, CompiledFile>.Empty.Add(
                "Program.cs",
                new CompiledFile(
                [
                    new CompiledFileOutput
                    {
                        Type = type,
                        Label = type == "cs" ? "C#" : "IL",
                        Language = language,
                        EagerText = text,
                    },
                ])),
            GlobalOutputs: [],
            NumWarnings: 0,
            NumErrors: errors,
            Diagnostics: [],
            BaseDirectory: "/");

    private sealed class FakeOutputLoadHost : IOutputLoadHost
    {
        public string ActiveSource { get; set; } = "Program.cs";
        public string ActiveOutput { get; set; } = "cs";
        public bool Running { get; set; }
        public CompiledAssembly? Compiled { get; set; }
        public CompilationInput? LastInput { get; set; }
        public bool StoreInCache { get; set; }
        public int CompileGeneration { get; set; }
        public int StoredCount { get; private set; }

        public bool IsCurrentCompile(int generation) => generation == CompileGeneration;

        public string OutputLabel(string tab) => tab == "tree" ? "Tree" : tab;

        public void Notify()
        {
        }

        public ValueTask<CompiledFileLazyResult> LoadFromWorkerAsync(string? file, string tab)
            => ValueTask.FromResult(new CompiledFileLazyResult { Text = "" });

        public void StoreCompiledOutput(CompiledAssembly compiled)
        {
            StoredCount++;
            _ = compiled;
        }
    }
}
