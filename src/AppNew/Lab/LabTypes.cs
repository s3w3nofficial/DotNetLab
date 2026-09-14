namespace DotNetLab.Lab;

public sealed record SdkOption(string Value, string Label, string Roslyn, string Razor)
{
    public string Released
    {
        get
        {
            const string marker = " — ";
            var index = Label.IndexOf(marker, StringComparison.Ordinal);
            return index >= 0 ? Label[(index + marker.Length)..] : string.Empty;
        }
    }
}

public sealed record OutputTab(string Type, string Label);

public enum OutputFileKind
{
    Cs,
    Razor,
    Cshtml
}

public enum DropZone
{
    Center,
    Left,
    Right,
    Top,
    Bottom
}

public readonly record struct TabRename(string From, string To);
