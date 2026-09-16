using BlazorMonaco.Editor;

namespace DotNetLab.Editor;

public interface ILabEditor
{
    event Action? Changed;

    event Func<Task>? SnapshotRequested;

    string MonacoTheme { get; }

    bool ResolvedDark { get; }

    bool DisableInputVirtualKeyboard { get; }

    bool UseVim { get; }

    void SetSource(string file, string contents);

    Task DetachEditorAsync(string editorId);

    Task OnEditorReadyAsync(string editorId, string? modelUri, bool readOnly, bool fold);

    Task EnableSemanticHighlightingAsync();

    Task InitializeLanguageServicesAsync();

    Task OnSourceModelContentChangedAsync(string modelUri, ModelContentChangedEvent args);

    Task PersistUrlAsync(bool snapshot = false);

    Task CompileAsync();
}
