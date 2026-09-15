namespace DotNetLab.Lab;

public interface ILabCommands
{
    event Action? Changed;

    bool Busy { get; }

    bool CompilerLoading { get; }

    bool MemoryUsageView { get; }

    bool DisableInputVirtualKeyboard { get; }

    bool Stacked { get; }

    string Sdk { get; }

    string Roslyn { get; }

    string Razor { get; }

    IReadOnlyList<SdkOption> AvailableSdks { get; }

    Task CompileAsync();

    Task ApplySdk(string value);

    Task SetRoslyn(string value);

    Task SetRazor(string value);

    void SetRazorToolchain(string value);

    void SetRazorStrategy(string value);

    Task ShowSettingsAsync();

    Task ShowPaletteAsync();

    void ToggleInputVirtualKeyboard();

    void ToggleStacked();

    Task EnsureSdkVersionsAsync();
}
