namespace DotNetLab.Features.Outputs;

public interface ILabOutputHost
{
    string ActiveSource { get; }

    string ActiveOutput { get; set; }

    void PublishOutputs(string? activeOutput = null);

    void Notify();
}
