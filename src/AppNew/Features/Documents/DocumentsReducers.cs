using Fluxor;

namespace DotNetLab.Features.Documents;

public static class DocumentsReducers
{
    [ReducerMethod]
    public static DocumentsState Reduce(DocumentsState _, SetDocumentsAction action)
        => action.Snapshot;
}
