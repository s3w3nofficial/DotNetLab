namespace DotNetLab.Lab;

public interface ILabPalette
{
    event Action? Changed;

    Task CompileAsync();

    Task FormatActiveSource();

    Task ShowPasteUrlAsync();

    Task ShowSettingsAsync();
}
