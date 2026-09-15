namespace DotNetLab.Lab;

/// <summary>
/// Compile session snapshot (<see cref="CompilationState.Running"/> /
/// <see cref="CompilationState.Stale"/>). Worker compile and
/// <see cref="CompiledAssembly"/> stay on <see cref="LabWorkspaceState"/>.
/// </summary>
public sealed class CompilationStore : StateStore<CompilationState>
{
    public CompilationStore() : base(new CompilationState())
    {
    }
}
