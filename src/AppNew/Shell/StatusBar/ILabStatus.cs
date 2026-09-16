namespace DotNetLab.Shell.StatusBar;

public interface ILabStatus
{
    event Action? Changed;

    string[] SourceCursor { get; }

    string[] Diagnostics { get; }
}
