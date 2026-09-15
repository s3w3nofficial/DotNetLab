namespace DotNetLab.Lab;

/// <summary>
/// UI log-level switch, same role as <c>src/App/Logging.cs</c>.
/// Worker containers snapshot this when they are created or recreated.
/// </summary>
public sealed class LabLogging
{
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
}
