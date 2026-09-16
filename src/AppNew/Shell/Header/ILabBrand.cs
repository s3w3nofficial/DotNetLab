namespace DotNetLab.Shell.Header;

public interface ILabBrand
{
    event Action? Changed;

    string Template { get; }

    void SetTemplate(string template);

    Task ShowPasteUrlAsync();

    Task ShowSettingsAsync();
}
