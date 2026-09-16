namespace DotNetLab.Shell.Header;

public interface ILabCommands
{
    event Action? Changed;

    bool Busy { get; }

    bool CompilerLoading { get; }

    Task CompileAsync();

    void SetRazorToolchain(string value);

    void SetRazorStrategy(string value);

    Task ShowSettingsAsync();

    Task ShowPaletteAsync();
}
