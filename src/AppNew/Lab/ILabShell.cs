namespace DotNetLab.Lab;

public interface ILabShell
{
    event Func<Task>? SettingsRequested;

    event Func<Task>? PaletteRequested;

    event Func<Task>? PasteUrlRequested;

    Task LoadSettingsAsync();
}
