namespace DotNetLab.Lab;

public interface ILabPalette
{
    event Action? Changed;

    bool Stacked { get; }

    bool ResolvedDark { get; }

    Task CompileAsync();

    Task FormatActiveSource();

    Task ShowPasteUrlAsync();

    Task ShowSettingsAsync();

    void ToggleWordWrap();

    void ToggleVim();

    void ToggleHintSquiggles();

    void ToggleStacked();
}
