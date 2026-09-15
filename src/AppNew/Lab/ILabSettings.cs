namespace DotNetLab.Lab;

public interface ILabSettings
{
    event Action? Changed;

    string RazorToolchain { get; set; }

    string RazorStrategy { get; set; }

    string ActiveSource { get; }

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
