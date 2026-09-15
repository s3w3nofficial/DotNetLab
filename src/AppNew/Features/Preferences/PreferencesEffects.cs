using DotNetLab.Lab;
using Fluxor;

namespace DotNetLab.Features.Preferences;

public sealed class PreferencesEffects(LabSettings settings, IState<PreferencesState> state, LabLogging logging)
{
    [EffectMethod]
    public Task Handle(SeedPreferencesAction _, IDispatcher dispatcher)
    {
        _ = dispatcher;
        ApplyLogLevel();
        return Task.CompletedTask;
    }

    [EffectMethod]
    public Task Handle(HydratePreferencesAction _, IDispatcher dispatcher)
    {
        _ = dispatcher;
        ApplyLogLevel();
        return Task.CompletedTask;
    }

    [EffectMethod]
    public Task Handle(PreferencesReadyAction _, IDispatcher dispatcher)
    {
        _ = dispatcher;
        ApplyLogLevel();
        return Task.CompletedTask;
    }

    [EffectMethod]
    public Task Handle(SetDebugLogsAction _, IDispatcher dispatcher)
    {
        _ = dispatcher;
        ApplyLogLevel();
        return Task.CompletedTask;
    }

    [EffectMethod]
    public Task Handle(SetTraceLogsAction _, IDispatcher dispatcher)
    {
        _ = dispatcher;
        ApplyLogLevel();
        return Task.CompletedTask;
    }

    [EffectMethod]
    public async Task Handle(PersistPreferencesAction _, IDispatcher dispatcher)
    {
        _ = dispatcher;
        var prefs = state.Value;
        if (!prefs.Ready)
        {
            return;
        }

        var snapshot = prefs.ToSnapshot();
        snapshot.CompilationPreferences = settings.CompilationPreferences;
        await settings.SaveAsync(snapshot);
    }

    private void ApplyLogLevel()
    {
        var prefs = state.Value;
        logging.LogLevel = prefs.TraceLogs
            ? LogLevel.Trace
            : prefs.DebugLogs
                ? LogLevel.Debug
                : LogLevel.Information;
    }
}
