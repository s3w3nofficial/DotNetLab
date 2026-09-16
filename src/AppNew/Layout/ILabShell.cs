namespace DotNetLab.Layout;

public interface ILabShell
{
    event Func<Task>? SettingsRequested;

    event Func<Task>? PaletteRequested;

    event Func<Task>? PasteUrlRequested;

    Task LoadSettingsAsync();

    Task ShowSettingsAsync();

    Task ShowPasteUrlAsync();

    Task ShowPaletteAsync();
}
