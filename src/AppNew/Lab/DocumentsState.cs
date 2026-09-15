namespace DotNetLab.Lab;

public sealed record DocumentsState
{
    public string Template { get; init; } = "C#";
    public string ActiveSource { get; init; } = InitialCode.CSharp.SuggestedFileName;
    public IReadOnlyList<string> SourceFiles { get; init; } = [InitialCode.CSharp.SuggestedFileName];
    public IReadOnlyDictionary<string, string> ModelUris { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [InitialCode.CSharp.SuggestedFileName] = CompiledAssembly.GetInputModelUri(InitialCode.CSharp.SuggestedFileName),
    };
}
