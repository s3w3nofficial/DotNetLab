namespace DotNetLab.Lab;

public interface ILabEnvironment
{
    bool IsDevelopment { get; }

    string BaseAddress { get; }
}

public sealed record LabEnvironment(bool IsDevelopment, string BaseAddress) : ILabEnvironment;
