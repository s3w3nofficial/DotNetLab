namespace DotNetLab.Lab;

public interface ILabSettings
{
    event Action? Changed;

    bool WordWrap { get; set; }

    bool UseVim { get; set; }

    bool LanguageServices { get; set; }

    bool DebugLogs { get; set; }

    bool TraceLogs { get; set; }

    bool MemoryUsageView { get; set; }

    bool BackgroundWorker { get; set; }

    bool EnableCaching { get; set; }

    bool AutomaticCompilation { get; set; }

    string RazorToolchain { get; set; }

    string RazorStrategy { get; set; }

    string AppTheme { get; }

    string ActiveSource { get; }

    void OnUiSettingsChanged();

    void OnSavedStateChanged();

    Task SetLanguageServicesAsync(bool enabled);

    Task ReloadWorkerAsync();

    Task PersistUrlAsync(bool snapshot = false);

    IReadOnlyList<OutputTab> SettingsRowsFor(OutputFileKind kind);

    bool IsOutputTabVisible(OutputFileKind kind, string type);

    bool CanMoveOutputTab(OutputFileKind kind, string type, int delta);

    void SetOutputTabVisible(OutputFileKind kind, string type, bool visible);

    void MoveOutputTab(OutputFileKind kind, string type, int delta);

    void ResetOutputTabs(OutputFileKind kind);

    Task PersistOutputTabsAsync();
}
