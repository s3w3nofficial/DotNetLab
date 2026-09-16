using Fluxor;

namespace DotNetLab.Features.Outputs;

public static class OutputsReducers
{
    [ReducerMethod]
    public static OutputsState Reduce(OutputsState _, SetOutputsAction action)
        => action.Snapshot;
}
