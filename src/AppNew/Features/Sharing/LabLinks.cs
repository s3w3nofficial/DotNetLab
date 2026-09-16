using DotNetLab.Features.Workspace;

namespace DotNetLab.Features.Sharing;

public static class LabLinks
{
    public const string Repository = "https://github.com/s3w3nofficial/DotNetLabRedesign";
    public const string Releases = $"{Repository}/releases";
    public const string NativeApps = "https://github.com/jjonescz/DotNetLab/blob/main/docs/native-apps.md";
    public const string GistNew = "https://gist.github.com/";
    public const string GitHubApi = "https://api.github.com";

    public static string NewIssue(LabWorkspaceState state)
    {
        var body = $"""
            ### Environment
            - Template: {state.Template}
            - SDK: {state.Sdk}
            - Roslyn: {state.Roslyn} ({state.RoslynConfig})
            - Razor: {state.Razor} ({state.RazorConfig})
            - Theme: {state.AppTheme}

            ### Description


            """;

        return $"{Repository}/issues/new?title={Uri.EscapeDataString("[.NET Lab] ")}&body={Uri.EscapeDataString(body)}";
    }

    public static string GistSnapshot(LabWorkspaceState state)
    {
        var text = new StringBuilder();
        text.AppendLine($"// .NET Lab snapshot · SDK {state.Sdk} · Roslyn {state.Roslyn} · Razor {state.Razor}");
        text.AppendLine();
        foreach (var file in state.SourceFiles)
        {
            text.AppendLine($"=== {file} ===");
            text.AppendLine(state.Sources.GetValueOrDefault(file, ""));
            text.AppendLine();
        }

        return text.ToString();
    }

    public static bool TryParseGistId(string url, out string gistId)
    {
        gistId = "";
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var trimmed = url.Trim();
        if (LooksLikeGistId(trimmed))
        {
            gistId = trimmed;
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        if (!host.Equals("gist.github.com", StringComparison.OrdinalIgnoreCase) &&
            !host.Equals("gist.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (LooksLikeGistId(part))
            {
                gistId = part;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeGistId(string value)
        => value.Length is >= 8 and <= 40 && value.All(char.IsAsciiHexDigit);
}
