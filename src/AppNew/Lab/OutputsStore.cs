namespace DotNetLab.Lab;

/// <summary>
/// Output-tab snapshot (<see cref="OutputsState.ActiveOutput"/> / open tab
/// ids / revision). <see cref="OutputTabLayout"/> still mutates; worker
/// output text and <see cref="CompiledAssembly"/> stay on
/// <see cref="LabWorkspaceState"/>.
/// </summary>
public sealed class OutputsStore : StateStore<OutputsState>
{
    public OutputsStore() : base(new OutputsState())
    {
    }
}
