using Fluxor;

namespace DotNetLab.Features.Compilation;

public static class CompilationReducers
{
    [ReducerMethod]
    public static CompilationState Reduce(CompilationState state, SetRunningAction action)
        => state with { Running = action.Value };

    [ReducerMethod]
    public static CompilationState Reduce(CompilationState state, SetStaleAction action)
        => state with { Stale = action.Value };

    [ReducerMethod]
    public static CompilationState Reduce(CompilationState state, SetDiagnosticCountsAction action)
        => state with { ErrorCount = action.ErrorCount, WarningCount = action.WarningCount };
}
