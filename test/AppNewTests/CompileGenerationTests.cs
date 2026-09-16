using AwesomeAssertions;
using DotNetLab.Features.Compilation;
using DotNetLab.Features.Compiler;
using DotNetLab.Features.Documents;
using DotNetLab.Features.Workspace;
using DotNetLab.Shell.StatusBar;

namespace DotNetLab;

[TestClass]
public sealed class CompileGenerationTests
{
    [TestMethod]
    public void GenerationCounter_StaleSnapshotsLose()
    {
        var counter = new GenerationCounter();
        counter.Current.Should().Be(0);

        var first = counter.Begin();
        first.Should().Be(1);
        counter.IsCurrent(first).Should().BeTrue();

        var second = counter.Begin();
        second.Should().Be(2);
        counter.IsCurrent(first).Should().BeFalse();
        counter.IsCurrent(second).Should().BeTrue();
    }

    [TestMethod]
    public void CompilationReducers_SetRunningAndStale()
    {
        var state = new CompilationState();
        state.Stale.Should().BeTrue();
        state.Running.Should().BeFalse();

        CompilationReducers.Reduce(state, new SetRunningAction(true)).Running.Should().BeTrue();
        CompilationReducers.Reduce(state, new SetStaleAction(false)).Stale.Should().BeFalse();
    }

    [TestMethod]
    public void StatusSelectors_SourceAndOutput()
    {
        var stale = new CompilationState { Stale = true };
        StatusSelectors.SourceRight(stale).Should().Contain("Modified");
        StatusSelectors.SourceReady(stale).Should().BeFalse();
        StatusSelectors.SourceReady(new CompilationState { Stale = false }).Should().BeTrue();
        StatusSelectors.OutputReady(new CompilationState { Running = true }).Should().BeFalse();

        var compiler = new CompilerState { Sdk = "built-in", Roslyn = "built-in" };
        StatusSelectors.OutputRight(compiler).Should().Contain(".NET");

        var documents = new DocumentsState();
        StatusSelectors.Left("source", documents, 3, 5, 1, 2).Should().Equal(
            "Ln 3, Col 5", "Spaces: 4", "UTF-8", "C#", "1 error", "2 warnings");
        StatusSelectors.Left("output", documents, 3, 5, 0, 1).Should().Equal(
            "Program.cs", "1 warning");
    }
}
