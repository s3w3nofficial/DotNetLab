namespace DotNetLab.Lab;

/// <summary>
/// Compiler selection snapshot (SDK / Roslyn / Razor). Worker apply stays on
/// <see cref="LabWorkspaceState"/>; this store does not hold worker handles.
/// </summary>
public sealed class CompilerStore : StateStore<CompilerState>
{
    public CompilerStore() : base(new CompilerState())
    {
    }
}
