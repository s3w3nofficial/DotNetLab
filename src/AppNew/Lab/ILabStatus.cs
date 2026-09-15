namespace DotNetLab.Lab;

public interface ILabStatus
{
    event Action? Changed;

    string[] SourceStatusLeft { get; }

    string SourceStatusRight { get; }

    string[] OutputStatusLeft { get; }

    string OutputStatusRight { get; }

    bool SourceReady { get; }

    bool OutputReady { get; }
}
