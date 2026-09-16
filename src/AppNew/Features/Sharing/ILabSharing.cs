using DotNetLab.Lab;

namespace DotNetLab.Features.Sharing;

public interface ILabSharing
{
    event Func<Task>? UrlPersistRequested;

    string Template { get; }

    string Sdk { get; }

    string Roslyn { get; }

    string RoslynConfig { get; }

    string Razor { get; }

    string RazorConfig { get; }

    string AppTheme { get; }

    IReadOnlyList<string> SourceFiles { get; }

    IReadOnlyDictionary<string, string> Sources { get; }

    bool EditingUserPreferences { get; set; }

    Task PersistUrlAsync(bool snapshot = false);

    Task SnapshotEditorsAsync();

    SavedState CaptureSavedState();

    Task ApplySavedStateAsync(SavedState state);
}
