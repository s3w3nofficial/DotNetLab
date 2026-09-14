using System.Text;
using System.Text.Json;

namespace DotNetLab.Lab;

public sealed class LabWorkspaceState
{
    public LabWorkspaceState()
    {
        Documents = new LabDocuments(this);
        Tabs = new OutputTabLayout(this);
    }

    public LabDocuments Documents { get; }
    public OutputTabLayout Tabs { get; }

    public event Action? Changed;
    public event Func<Task>? SettingsRequested;
    public event Func<Task>? PaletteRequested;
    public event Func<Task>? PasteUrlRequested;

    public bool Stacked { get; private set; }
    public double Split { get; private set; } = 50;
    public bool Running { get; private set; }
    public bool Stale { get; internal set; } = true;
    public string Template => Documents.Template;

    public string ActiveSource
    {
        get => Documents.ActiveSource;
        set => Documents.ActiveSource = value;
    }

    public string ActiveOutput { get; set; } = "cs";
    public string Sdk { get; private set; } = LabCatalog.SdkVersions[0].Value;
    public string Roslyn { get; private set; } = "built-in";
    public string RoslynConfig { get; set; } = "Release";
    public string Razor { get; private set; } = "built-in";
    public string RazorConfig { get; set; } = "Release";
    public string RazorToolchain { get; set; } = "Auto";
    public string RazorStrategy { get; set; } = "Runtime";
    public bool WordWrap { get; set; }
    public bool UseVim { get; set; }
    public bool DisableInputVirtualKeyboard { get; private set; }
    public bool LanguageServices { get; set; } = true;
    public bool DebugLogs { get; set; }
    public bool TraceLogs { get; set; }
    public bool MemoryUsageView { get; set; }
    public bool BackgroundWorker { get; set; } = true;
    public bool EnableCaching { get; set; } = true;
    public bool AutomaticCompilation { get; set; } = true;
    public string AppTheme { get; private set; } = "dark";
    public bool ResolvedDark { get; private set; } = true;
    public string MonacoTheme => LabTheme.MonacoThemeName(ResolvedDark);
    public bool DecodeCustomAttributeBlobs { get; set; }
    public bool ShowSequencePoints { get; set; }
    public bool FullIl { get; set; }
    public string ShowSymbols { get; set; } = "No Symbols";
    public bool ShowOperations { get; set; }
    public bool ShowBoundNodes { get; set; }
    public bool ShowDeclarationDocument { get; set; }
    public bool ShowRenderedHtml { get; set; }
    public bool ExcludeSingleFileNameInDiagnostics { get; set; } = true;
    public bool IncludeHiddenDiagnostics { get; set; }
    public int CursorLine { get; set; } = 9;
    public int CursorColumn { get; set; } = 34;
    public string UpdateState { get; private set; } = "idle";

    public Dictionary<string, string> Sources => Documents.Sources;
    public List<string> SourceFiles => Documents.SourceFiles;
    public SdkOption ResolvedSdk => LabCatalog.SdkVersions.FirstOrDefault(item => item.Value == Sdk) ?? LabCatalog.SdkVersions[0];
    public int OutputLayoutRevision => Tabs.Revision;

    public IReadOnlyList<string> CurrentOutputTabIds => Tabs.CurrentOutputTabIds;
    public IReadOnlyList<OutputTab> CurrentOutputTabs => Tabs.CurrentOutputTabs;

    public static readonly SdkOption[] SdkVersions = LabCatalog.SdkVersions;
    public static readonly string[] CompilerRefs = LabCatalog.CompilerRefs;
    public static readonly string[] RazorToolchains = LabCatalog.RazorToolchains;
    public static readonly string[] RazorStrategies = LabCatalog.RazorStrategies;
    public static readonly string[] Templates = LabCatalog.Templates;
    public static readonly string[] SymbolDisplayKinds = LabCatalog.SymbolDisplayKinds;
    public const string ErrorsOutputType = LabCatalog.ErrorsOutputType;
    public const string DirectivesFileName = LabFixtures.DirectivesFileName;
    public const string ConfigurationFileName = LabFixtures.ConfigurationFileName;
    public static readonly string[] SpecialSourceOrder = LabFixtures.SpecialSourceOrder;

    public static string OutputKindLabel(OutputFileKind kind) => LabCatalog.OutputKindLabel(kind);
    public static OutputFileKind OutputKindFor(string fileName) => LabCatalog.OutputKindFor(fileName);
    public static bool IsOutputTabLocked(string type) => LabCatalog.IsOutputTabLocked(type);
    public static bool IsSpecialSource(string fileName) => LabDocuments.IsSpecialSource(fileName);
    public static string DisplayName(string fileName) => LabDocuments.DisplayName(fileName);
    public static bool IsRazorLike(string fileName) => LabCatalog.IsRazorLike(fileName);

    public void Notify() => Changed?.Invoke();

    public IReadOnlyList<OutputTab> SettingsRowsFor(OutputFileKind kind) => Tabs.SettingsRowsFor(kind);
    public bool IsOutputTabVisible(OutputFileKind kind, string type) => Tabs.IsOutputTabVisible(kind, type);
    public bool CanMoveOutputTab(OutputFileKind kind, string type, int delta) => Tabs.CanMoveOutputTab(kind, type, delta);
    public void SetOutputTabVisible(OutputFileKind kind, string type, bool visible) => Tabs.SetOutputTabVisible(kind, type, visible);
    public void MoveOutputTab(OutputFileKind kind, string type, int delta) => Tabs.MoveOutputTab(kind, type, delta);
    public void ResetOutputTabs(OutputFileKind kind) => Tabs.ResetOutputTabs(kind);
    public string SerializeOutputTabs() => Tabs.SerializeOutputTabs();
    public void ApplySavedOutputTabs(string? json) => Tabs.ApplySavedOutputTabs(json);
    public void CaptureOpenOutputTabs(IReadOnlyList<string> ids) => Tabs.CaptureOpenOutputTabs(ids);
    public IReadOnlyList<OutputTab> AddableOutputTabsFor(IReadOnlyList<string> open) => Tabs.AddableOutputTabsFor(open);
    public bool HasClosedOutputTabs(IReadOnlyList<string> open) => Tabs.HasClosedOutputTabs(open);
    public bool OutputTabOrderDiffers(IReadOnlyList<string> open) => Tabs.OutputTabOrderDiffers(open);
    public void AddOutputTab(string type) => Tabs.AddOutputTab(type);
    public void RestoreOutputTabOrder() => Tabs.RestoreOutputTabOrder();
    public void RestoreClosedOutputTabs() => Tabs.RestoreClosedOutputTabs();
    public void SaveOpenOutputTabsAsSettings() => Tabs.SaveOpenOutputTabsAsSettings();
    public void EnsureActiveOutput() => Tabs.EnsureActiveOutput();
    public IReadOnlyList<OutputTab> OutputTabsFor(string fileName) => Tabs.OutputTabsFor(fileName);
    public string OutputLabel(string type) => Tabs.OutputLabel(type);
    public string OutputTabTitle(string type) => Tabs.OutputTabTitle(type);

    public void SetTheme(string preference, bool resolvedDark)
    {
        preference = LabTheme.NormalizePreference(preference);
        if (AppTheme == preference && ResolvedDark == resolvedDark)
        {
            return;
        }

        AppTheme = preference;
        ResolvedDark = resolvedDark;
        Notify();
    }

    public Task ShowSettingsAsync() => SettingsRequested?.Invoke() ?? Task.CompletedTask;
    public Task ShowPaletteAsync() => PaletteRequested?.Invoke() ?? Task.CompletedTask;
    public Task ShowPasteUrlAsync() => PasteUrlRequested?.Invoke() ?? Task.CompletedTask;

    public void ToggleWordWrap()
    {
        WordWrap = !WordWrap;
        Notify();
    }

    public void ToggleVim()
    {
        UseVim = !UseVim;
        Notify();
    }

    public void SetStacked(bool stacked)
    {
        Stacked = stacked;
        Notify();
    }

    public void ToggleStacked() => SetStacked(!Stacked);

    public void ToggleInputVirtualKeyboard()
    {
        DisableInputVirtualKeyboard = !DisableInputVirtualKeyboard;
        Notify();
    }

    public void SetSplit(double value, bool notify = true)
    {
        var next = Math.Clamp(value, 25, 75);
        if (Math.Abs(next - Split) < 0.05)
        {
            return;
        }

        Split = next;
        if (notify)
        {
            Notify();
        }
    }

    public void SetTemplate(string template) => Documents.SetTemplate(template);

    public void ApplySdk(string value)
    {
        var found = LabCatalog.SdkVersions.FirstOrDefault(item => item.Value == value) ?? LabCatalog.SdkVersions[0];
        Sdk = found.Value;
        Roslyn = found.Roslyn;
        Razor = found.Razor;
        Stale = true;
        Notify();
    }

    public void SetRoslyn(string value)
    {
        Roslyn = value;
        Stale = true;
        Notify();
    }

    public void SetRazor(string value)
    {
        Razor = value;
        Stale = true;
        Notify();
    }

    public void MarkStale()
    {
        Stale = true;
        Notify();
    }

    public void SetSource(string file, string contents) => Documents.SetSource(file, contents);
    public void RenameFile(string oldName, string newName) => Documents.RenameFile(oldName, newName);
    public void CloseFile(string file) => Documents.CloseFile(file);
    public void AddFile(string extension) => Documents.AddFile(extension);
    public void OpenDirectives() => Documents.OpenDirectives();
    public void OpenConfiguration() => Documents.OpenConfiguration();
    public void LoadImportedFiles(IReadOnlyDictionary<string, string> files) => Documents.LoadImportedFiles(files);
    public void FormatActiveSource() => Documents.FormatActiveSource();
    public void SetActiveSource(string file) => Documents.SetActiveSource(file);

    public async Task CompileAsync()
    {
        if (Running)
        {
            return;
        }

        Running = true;
        Notify();
        await Task.Delay(900);
        Running = false;
        Stale = false;
        Notify();
    }

    public async Task CheckUpdatesAsync()
    {
        UpdateState = "checking";
        Notify();
        await Task.Delay(900);
        UpdateState = "available";
        Notify();
    }

    public string OutputLanguage(string type) => LabCatalog.OutputLanguage(type);
    public string LanguageFor(string fileName) => LabCatalog.LanguageFor(fileName);

    public string GetOutput(string tab)
    {
        if (Running)
        {
            return "Compiling…";
        }

        return tab switch
        {
            "run" => "Hello, .NET Lab!\nProcess exited with code 0",
            "il" => LabFixtures.IlOutput,
            "seq" => LabFixtures.SeqOutput,
            "cs" => LabFixtures.DecompiledCSharp,
            "gcs" => LabFixtures.GeneratedRazorCSharp,
            "tree" => LabFixtures.TreeOutput,
            "syntax" => LabFixtures.RazorSyntaxOutput,
            "ir" => LabFixtures.RazorIrOutput,
            "html" => ActiveSource.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
                ? "Rendering Razor Pages (.cshtml) to HTML is currently not supported. Try Razor Components (.razor) instead."
                : LabFixtures.RazorHtmlOutput,
            LabCatalog.RazorErrorsOutputType => "No Razor diagnostics.",
            "asm" => "JIT disassembler is not available on this platform (it's only available in a native app).",
            "xml" => LabFixtures.DocsOutput,
            "errors" => GetErrorsOutput(),
            _ => $"{OutputLabel(tab)} output\n"
        };
    }

    public string GetErrorsOutput()
    {
        var location = ExcludeSingleFileNameInDiagnostics
            ? "(4,28)"
            : "Program.cs(4,28)";

        return $"""
            // {location}: error CS1002: ; expected
            // Console.WriteLine("Hello, .NET Lab!")
            Diagnostic(ErrorCode.ERR_SemicolonExpected, "").WithLocation(4, 28)
            """;
    }
}
