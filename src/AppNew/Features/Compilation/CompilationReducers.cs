using DotNetLab.Features.Compiler;
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

    [ReducerMethod]
    public static CompilationState Reduce(CompilationState state, ApplySdkAction _)
        => MarkStale(state);

    [ReducerMethod]
    public static CompilationState Reduce(CompilationState state, SdkApplyStartedAction _)
        => MarkStale(state);

    [ReducerMethod]
    public static CompilationState Reduce(CompilationState state, SdkResolvedAction _)
        => MarkStale(state);

    [ReducerMethod]
    public static CompilationState Reduce(CompilationState state, RestoreCompilersAction _)
        => MarkStale(state);

    [ReducerMethod]
    public static CompilationState Reduce(CompilationState state, CompilerApplyStartedAction _)
        => MarkStale(state);

    private static CompilationState MarkStale(CompilationState state)
        => state.Stale ? state : state with { Stale = true };
}
