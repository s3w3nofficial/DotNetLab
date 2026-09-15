using Fluxor;

namespace DotNetLab.Features.Compilation;

[FeatureState]
public sealed record CompilationState
{
    public bool Running { get; init; }
    public bool Stale { get; init; } = true;

    public CompilationState()
    {
    }
}
