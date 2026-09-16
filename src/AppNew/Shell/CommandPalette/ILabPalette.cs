namespace DotNetLab.Shell.CommandPalette;

public interface ILabPalette
{
    event Action? Changed;

    Task CompileAsync();

    Task FormatActiveSource();

    Task ShowPasteUrlAsync();

    Task ShowSettingsAsync();
}
