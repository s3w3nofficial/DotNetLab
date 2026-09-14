namespace DotNetLab.Lab;

public sealed class LabDocuments
{
    private readonly LabWorkspaceState _state;

    public LabDocuments(LabWorkspaceState state)
    {
        _state = state;
    }

    public string Template { get; private set; } = "C#";
    public string ActiveSource { get; set; } = "Program.cs";

    public Dictionary<string, string> Sources { get; } = new(StringComparer.Ordinal)
    {
        ["Program.cs"] = LabFixtures.DefaultProgram,
    };

    public List<string> SourceFiles { get; } = ["Program.cs"];

    public static bool IsSpecialSource(string fileName)
        => fileName is LabFixtures.DirectivesFileName or LabFixtures.ConfigurationFileName;

    public static string DisplayName(string fileName)
        => fileName switch
        {
            LabFixtures.DirectivesFileName => "Directives",
            LabFixtures.ConfigurationFileName => "Configuration",
            _ => fileName
        };

    public void SetTemplate(string template)
    {
        Template = template;

        foreach (var file in SourceFiles.Where(name => !IsSpecialSource(name)).ToArray())
        {
            SourceFiles.Remove(file);
            Sources.Remove(file);
        }

        foreach (var (name, contents) in FilesFor(template))
        {
            InsertUserFile(name);
            Sources[name] = contents;
        }

        if (template is "Razor")
        {
            ActiveSource = "TestComponent.razor";
            _state.ActiveOutput = "gcs";
        }
        else if (template is "CSHTML")
        {
            ActiveSource = "TestPage.cshtml";
            _state.ActiveOutput = "gcs";
        }
        else
        {
            ActiveSource = "Program.cs";
            _state.ActiveOutput = "cs";
        }

        _state.Stale = true;
        _state.Tabs.EnsureActiveOutput();
        _state.Notify();
    }

    private static (string Name, string Contents)[] FilesFor(string template)
        => template switch
        {
            "Razor" =>
            [
                ("TestComponent.razor", LabFixtures.DefaultRazor),
                ("_Imports.razor", LabFixtures.DefaultRazorImports),
            ],
            "CSHTML" =>
            [
                ("TestPage.cshtml", LabFixtures.DefaultCshtml),
            ],
            _ =>
            [
                ("Program.cs", LabFixtures.DefaultProgram),
            ],
        };

    public void SetSource(string file, string contents)
    {
        Sources[file] = contents;
        _state.Stale = true;
        _state.Notify();
    }

    public void RenameFile(string oldName, string newName)
    {
        if (!TryNormalizeRename(oldName, newName, out var normalized))
        {
            return;
        }

        var index = SourceFiles.IndexOf(oldName);
        if (index < 0)
        {
            return;
        }

        SourceFiles[index] = normalized;
        if (Sources.Remove(oldName, out var contents))
        {
            Sources[normalized] = contents;
        }

        if (string.Equals(ActiveSource, oldName, StringComparison.Ordinal))
        {
            ActiveSource = normalized;
            _state.Tabs.EnsureActiveOutput();
        }

        _state.Stale = true;
        _state.Notify();
    }

    private bool TryNormalizeRename(string oldName, string newName, out string normalized)
    {
        normalized = (newName ?? "").Trim();
        if (string.IsNullOrEmpty(normalized) ||
            string.Equals(oldName, normalized, StringComparison.Ordinal) ||
            IsSpecialSource(oldName) ||
            IsSpecialSource(normalized) ||
            normalized is "." or ".." ||
            normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(Path.GetFileName(normalized), normalized, StringComparison.Ordinal) ||
            !SourceFiles.Contains(oldName) ||
            SourceFiles.Contains(normalized))
        {
            normalized = "";
            return false;
        }

        return true;
    }

    public void CloseFile(string file)
    {
        if (SourceFiles.Count <= 1 || !SourceFiles.Remove(file))
        {
            return;
        }

        Sources.Remove(file);
        if (ActiveSource == file)
        {
            ActiveSource = SourceFiles.FirstOrDefault(name => !IsSpecialSource(name)) ?? SourceFiles[0];
            _state.Tabs.EnsureActiveOutput();
        }

        _state.Stale = true;
        _state.Notify();
    }

    public void AddFile(string extension)
    {
        var index = 1;
        string name;
        do
        {
            name = extension switch
            {
                ".razor" => $"Component{index}.razor",
                ".cshtml" => $"Page{index}.cshtml",
                _ => $"File{index}.cs"
            };
            index++;
        } while (SourceFiles.Contains(name));

        var contents = extension switch
        {
            ".razor" => $"<h1>{name}</h1>\n",
            ".cshtml" => $"@page \"/{Path.GetFileNameWithoutExtension(name).ToLowerInvariant()}\"\n\n<h1>{name}</h1>\n",
            _ => $"public class {Path.GetFileNameWithoutExtension(name)}\n{{\n}}\n"
        };

        InsertUserFile(name);
        Sources[name] = contents;
        ActiveSource = name;
        _state.Tabs.EnsureActiveOutput();
        _state.Stale = true;
        _state.Notify();
    }

    public void OpenDirectives() => OpenSpecialSource(LabFixtures.DirectivesFileName, LabFixtures.DefaultDirectives);

    public void OpenConfiguration() => OpenSpecialSource(LabFixtures.ConfigurationFileName, LabFixtures.DefaultConfiguration);

    private static bool IsUserCsharpFile(string fileName)
        => fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !IsSpecialSource(fileName);

    private void OpenSpecialSource(string fileName, string contents)
    {
        if (!SourceFiles.Contains(fileName))
        {
            InsertSpecialFile(fileName);
            Sources[fileName] = contents;
            _state.Stale = true;
        }

        ActiveSource = fileName;
        _state.Tabs.EnsureActiveOutput();
        _state.Notify();
    }

    private void InsertUserFile(string name)
    {
        var at = SourceFiles.FindIndex(IsSpecialSource);
        if (at < 0)
        {
            SourceFiles.Add(name);
            return;
        }

        SourceFiles.Insert(at, name);
    }

    private void InsertSpecialFile(string fileName)
    {
        var userCount = SourceFiles.FindIndex(IsSpecialSource);
        if (userCount < 0)
        {
            userCount = SourceFiles.Count;
        }

        var slot = Array.IndexOf(LabFixtures.SpecialSourceOrder, fileName);
        var at = userCount;
        for (var i = 0; i < slot; i++)
        {
            if (SourceFiles.Contains(LabFixtures.SpecialSourceOrder[i]))
            {
                at++;
            }
        }

        SourceFiles.Insert(at, fileName);
    }

    public void LoadImportedFiles(IReadOnlyDictionary<string, string> files)
    {
        var incoming = files
            .Select(pair => (Name: Path.GetFileName(pair.Key.Trim()), Contents: pair.Value ?? ""))
            .Where(pair =>
                !string.IsNullOrWhiteSpace(pair.Name) &&
                pair.Name is not "." and not ".." &&
                pair.Name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            .GroupBy(pair => pair.Name, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();

        if (incoming.Length == 0)
        {
            return;
        }

        foreach (var file in SourceFiles.Where(name => !IsSpecialSource(name)).ToArray())
        {
            SourceFiles.Remove(file);
            Sources.Remove(file);
        }

        foreach (var (name, contents) in incoming)
        {
            if (IsSpecialSource(name))
            {
                if (!SourceFiles.Contains(name))
                {
                    InsertSpecialFile(name);
                }

                Sources[name] = contents;
                continue;
            }

            InsertUserFile(name);
            Sources[name] = contents;
        }

        ActiveSource = SourceFiles.FirstOrDefault(name => !IsSpecialSource(name)) ?? SourceFiles[0];
        _state.Tabs.EnsureActiveOutput();
        _state.Stale = true;
        _state.Notify();
    }

    public void SetActiveSource(string file)
    {
        if (string.Equals(ActiveSource, file, StringComparison.Ordinal))
        {
            return;
        }

        ActiveSource = file;
        _state.Tabs.EnsureActiveOutput();
        _state.Notify();
    }
}
