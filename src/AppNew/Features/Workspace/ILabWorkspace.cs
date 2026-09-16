using DotNetLab.Features.Outputs;

namespace DotNetLab.Features.Workspace;

public interface ILabWorkspace
{
    event Action? Changed;

    bool Stacked { get; }

    double Split { get; }

    bool DisplayHintSquiggles { get; }

    bool WordWrap { get; }

    IReadOnlyList<string> SourceFiles { get; }

    string ActiveSource { get; }

    IReadOnlyDictionary<string, string> Sources { get; }

    string? WorkerError { get; }

    IReadOnlyList<string> CurrentOutputTabIds { get; }

    string DisplayOutputType { get; }

    int OutputLayoutRevision { get; }

    string ActiveOutput { get; set; }

    bool HasDiagnosticCounts { get; }

    int ErrorCount { get; }

    int WarningCount { get; }

    string ShowSymbols { get; set; }

    bool ShowOperations { get; set; }

    bool ShowBoundNodes { get; set; }

    bool ShowDeclarationDocument { get; set; }

    bool ShowRenderedHtml { get; set; }

    bool DecodeCustomAttributeBlobs { get; set; }

    bool ShowSequencePoints { get; set; }

    bool FullIl { get; set; }

    bool ExcludeSingleFileNameInDiagnostics { get; set; }

    bool IncludeHiddenDiagnostics { get; set; }

    int CursorLine { get; set; }

    int CursorColumn { get; set; }

    string UriFor(string fileName);

    string LanguageFor(string fileName);

    string GetOutput(string tab);

    string OutputLanguage(string type);

    string OutputUriFor(string tab);

    string OutputLabel(string type);

    string OutputTabTitle(string type);

    IReadOnlyList<OutputTab> AddableOutputTabsFor(IReadOnlyList<string> open);

    bool HasClosedOutputTabs(IReadOnlyList<string> open);

    bool OutputTabOrderDiffers(IReadOnlyList<string> open);

    void CloseFile(string file);

    void AddFile(string extension);

    void OpenDirectives();

    void OpenConfiguration();

    void SetActiveSource(string file);

    void RenameFile(string oldName, string newName);

    void CaptureOpenOutputTabs(IReadOnlyList<string> ids);

    void AddOutputTab(string type);

    void RestoreOutputTabOrder();

    void RestoreClosedOutputTabs();

    void SaveOpenOutputTabsAsSettings();

    void EnsureActiveOutput();

    void SetSplit(double value, bool notify = true);

    void Notify();

    void NotifyStatus();

    void OnSavedStateChanged();

    Task FormatActiveSource();

    Task ReloadWorkerAsync();

    Task PersistOutputTabsAsync();
}
