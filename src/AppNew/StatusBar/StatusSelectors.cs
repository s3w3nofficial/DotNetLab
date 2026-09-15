using DotNetLab.Features.Compilation;
using DotNetLab.Features.Compiler;

namespace DotNetLab.StatusBar;

public static class StatusSelectors
{
    public static string SourceRight(CompilationState compilation)
        => compilation.Stale ? "Modified · Ctrl+S to compile" : "Ready · Ctrl+S to compile";

    public static bool SourceReady(CompilationState compilation)
        => !compilation.Stale && !compilation.Running;

    public static string OutputRight(CompilerState compiler)
        => $".NET {compiler.Resolved.Value} · Roslyn {compiler.Roslyn}";

    public static bool OutputReady(CompilationState compilation)
        => !compilation.Running;

    public static string Right(string side, CompilationState compilation, CompilerState compiler)
        => IsOutput(side) ? OutputRight(compiler) : SourceRight(compilation);

    public static bool Ready(string side, CompilationState compilation)
        => IsOutput(side) ? OutputReady(compilation) : SourceReady(compilation);

    private static bool IsOutput(string side)
        => string.Equals(side, "output", StringComparison.Ordinal);
}
