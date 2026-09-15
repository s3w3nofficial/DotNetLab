namespace DotNetLab.Lab;

public interface ILabCommands
{
    event Action? Changed;

    bool Busy { get; }

    bool CompilerLoading { get; }

    bool MemoryUsageView { get; }

    bool DisableInputVirtualKeyboard { get; }

    bool Stacked { get; }

    Task CompileAsync();

    void SetRazorToolchain(string value);

    void SetRazorStrategy(string value);

    Task ShowSettingsAsync();

    Task ShowPaletteAsync();

    void ToggleInputVirtualKeyboard();

    void ToggleStacked();
}
