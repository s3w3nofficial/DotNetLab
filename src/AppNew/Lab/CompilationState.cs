namespace DotNetLab.Lab;

public sealed record CompilationState
{
    public bool Running { get; init; }
    public bool Stale { get; init; } = true;
}
