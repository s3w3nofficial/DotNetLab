namespace DotNetLab.Lab;

/// <summary>
/// UI preference snapshot (wrap, vim, logs, caching, theme, stacked, …).
/// Persist stays on <see cref="LabWorkspaceState"/>; this store does not hold
/// Monaco handles, the worker, or <see cref="LabSettings"/>.
/// </summary>
public sealed class PreferencesStore : StateStore<PreferencesState>
{
    public PreferencesStore(ILabEnvironment environment)
        : base(new PreferencesState { DebugLogs = environment.IsDevelopment })
    {
    }
}
