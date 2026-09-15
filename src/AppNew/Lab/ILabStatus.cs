namespace DotNetLab.Lab;

public interface ILabStatus
{
    event Action? Changed;

    string[] SourceStatusLeft { get; }

    string[] OutputStatusLeft { get; }
}
