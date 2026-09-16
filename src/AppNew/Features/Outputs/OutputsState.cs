using Fluxor;

namespace DotNetLab.Features.Outputs;

[FeatureState]
public sealed record OutputsState
{
    public string ActiveOutput { get; init; } = "cs";
    public int Revision { get; init; }
    public IReadOnlyList<string> CurrentOutputTabIds { get; init; } = [];

    public OutputsState()
    {
    }
}
