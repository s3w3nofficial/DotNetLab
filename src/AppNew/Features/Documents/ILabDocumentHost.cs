namespace DotNetLab.Features.Documents;

public interface ILabDocumentHost
{
    string ActiveOutput { get; set; }

    bool Stale { get; set; }

    void EnsureActiveOutput();

    void Notify();

    void NotifyStatus();

    Task AfterDocumentsChangedAsync(IReadOnlyList<string> before);

    Task PersistUrlAsync(bool snapshot = false);
}
