namespace DotNetLab.Lab;

/// <summary>
/// Document snapshot (<see cref="DocumentsState.Template"/> /
/// <see cref="DocumentsState.ActiveSource"/> / file list / model URIs).
/// File contents stay on <see cref="LabDocuments"/>; Monaco stays the source
/// of truth for buffer text.
/// </summary>
public sealed class DocumentsStore : StateStore<DocumentsState>
{
    public DocumentsStore() : base(new DocumentsState())
    {
    }
}
