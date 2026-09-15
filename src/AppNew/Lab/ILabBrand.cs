namespace DotNetLab.Lab;

public interface ILabBrand
{
    event Action? Changed;

    string Template { get; }

    bool ResolvedDark { get; }

    void SetTemplate(string template);

    Task ShowPasteUrlAsync();

    Task ShowSettingsAsync();
}
