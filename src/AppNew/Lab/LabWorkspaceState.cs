using System.Collections.Immutable;
using BlazorMonaco.Editor;
using DotNetLab.Features.Compiler;
using DotNetLab.Features.Compilation;
using DotNetLab.Features.Documents;
using DotNetLab.Features.Preferences;
using DotNetLab.Features.Workspace;
using Fluxor;
using Microsoft.JSInterop;

namespace DotNetLab.Lab;

public sealed class LabWorkspaceState : ILabStatus, ILabBrand, ILabCommands, ILabPalette, ILabSettings, ILabShell, IDisposable
{
    private readonly WorkerHost _worker;
    private readonly LabLanguageServices _language;
    private readonly LabCursorSync _cursors;
    private readonly LabSettings _settings;
    private readonly TemplateCache _templates;
    private readonly InputOutputCache _cache;
    private readonly IState<CompilerState> _compiler;
    private readonly IState<PreferencesState> _preferences;
    private readonly IState<CompilationState> _compilation;
    private readonly IState<DocumentsState> _documents;
    private readonly IState<WorkspaceState> _workspace;
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<LabWorkspaceState> _logger;
    private readonly Dictionary<string, OutputSnapshot> _outputCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _outputModelUris = new(StringComparer.Ordinal);
    private readonly HashSet<string> _outputLoading = new(StringComparer.Ordinal);
    private string _activeOutput = "cs";
    private bool _showErrorListIfOutputEmpty;
    private int _compileGeneration;
    private int _applyGeneration;
    private bool _suppressUrlPersist;
    private bool _settingsReady;
    private bool _storeInCache;
    private CompilationInput? _liveCompiledInput;
    private string? _compiledCompilerKey;
    private string _compilerKey;
    private bool _compilerWasLoading;
    private Task? _languageInit;

    public LabWorkspaceState(
        WorkerHost worker,
        LabLanguageServices language,
        LabCursorSync cursors,
        LabSettings settings,
        TemplateCache templates,
        InputOutputCache cache,
        IState<CompilerState> compiler,
        IState<PreferencesState> preferences,
        IState<CompilationState> compilation,
        IState<DocumentsState> documents,
        IState<WorkspaceState> workspace,
        IDispatcher dispatcher,
        ILogger<LabWorkspaceState> logger)
    {
        _worker = worker;
        _language = language;
        _cursors = cursors;
        _settings = settings;
        _templates = templates;
        _cache = cache;
        _compiler = compiler;
        _preferences = preferences;
        _compilation = compilation;
        _documents = documents;
        _workspace = workspace;
        _dispatcher = dispatcher;
        _logger = logger;
        Documents = new LabDocuments(this, dispatcher);
        Tabs = new OutputTabLayout(this);
        _compilerKey = Compiler.Key;
        _compiler.StateChanged += OnCompilerStoreChanged;
        _preferences.StateChanged += OnPreferencesChanged;
        _compilation.StateChanged += OnCompilationChanged;
        _documents.StateChanged += OnDocumentsChanged;
        _workspace.StateChanged += OnWorkspaceChanged;
        _worker.Failed += OnWorkerFailed;
    }

    public LabDocuments Documents { get; }
    public OutputTabLayout Tabs { get; }
    public CompiledAssembly? Compiled { get; private set; }
    public CompilationInput? LastInput { get; private set; }

    public event Action? Changed;
    public event Action? StatusChanged;

    event Action? ILabStatus.Changed
    {
        add
        {
            Changed += value;
            StatusChanged += value;
        }
        remove
        {
            Changed -= value;
            StatusChanged -= value;
        }
    }
    public event Func<Task>? SettingsRequested;
    public event Func<Task>? PaletteRequested;
    public event Func<Task>? PasteUrlRequested;
    public event Func<Task>? SnapshotRequested;
    public event Func<Task>? UrlPersistRequested;

    public bool Stacked => Preferences.Stacked;
    public double Split => WorkspaceSnapshot.Split;
    public bool Running
    {
        get => Compilation.Running;
        private set => _dispatcher.Dispatch(new SetRunningAction(value));
    }
    public bool Stale
    {
        get => Compilation.Stale;
        internal set => _dispatcher.Dispatch(new SetStaleAction(value));
    }
    public string Template => DocumentsSnapshot.Template;

    public string ActiveSource
    {
        get => DocumentsSnapshot.ActiveSource;
        set
        {
            Documents.ActiveSource = value;
            Documents.Publish();
        }
    }

    public string ActiveOutput
    {
        get => _activeOutput;
        set
        {
            var dismiss = _showErrorListIfOutputEmpty;
            _showErrorListIfOutputEmpty = false;
            if (string.Equals(_activeOutput, value, StringComparison.Ordinal))
            {
                if (dismiss)
                {
                    Notify();
                }

                return;
            }

            _activeOutput = value;
            _ = EnsureOutputLoadedAsync(value);
            _ = PersistUrlAsync();
        }
    }

    public string DisplayOutputType
        => _showErrorListIfOutputEmpty && HasEmptyOutputText(_activeOutput) == true
            ? ErrorsOutputType
            : _activeOutput;
    public string Sdk => Compiler.Sdk;
    public string Roslyn => Compiler.Roslyn;
    public string Razor => Compiler.Razor;
    public string RoslynConfig => Compiler.RoslynConfig;
    public string RazorConfig => Compiler.RazorConfig;
    public bool CompilerLoading => Compiler.Loading;
    public bool Busy => Running || CompilerLoading;
    public string RazorToolchain { get; set; } = "Auto";
    public string RazorStrategy { get; set; } = "Runtime";
    public bool WordWrap => Preferences.WordWrap;
    public bool UseVim => Preferences.UseVim;
    public bool DisableInputVirtualKeyboard => Preferences.DisableInputVirtualKeyboard;
    public bool LanguageServices => Preferences.LanguageServices;
    public bool DebugLogs => Preferences.DebugLogs;
    public bool TraceLogs => Preferences.TraceLogs;
    public bool MemoryUsageView => Preferences.MemoryUsageView;
    public bool BackgroundWorker => Preferences.BackgroundWorker;
    public bool DisplayHintSquiggles => Preferences.DisplayHintSquiggles;
    public bool EnableCaching => Preferences.EnableCaching;
    public bool AutomaticCompilation => Preferences.AutomaticCompilation;
    public string AppTheme => Preferences.AppTheme;
    public bool ResolvedDark => Preferences.ResolvedDark;
    public string MonacoTheme => Preferences.MonacoTheme;
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
    public string? WorkerError { get; private set; }
    public bool EditingUserPreferences { get; set; }
    public int ErrorCount => Compiled?.NumErrors ?? 0;
    public int WarningCount => Compiled?.NumWarnings ?? 0;
    public bool HasDiagnosticCounts => ErrorCount > 0 || WarningCount > 0;
    public string[] SourceCursor =>
    [
        $"Ln {CursorLine}, Col {CursorColumn}",
        "Spaces: 4",
        "UTF-8",
    ];
    public string[] Diagnostics => [.. DiagnosticStatusParts];

    private IEnumerable<string> DiagnosticStatusParts
    {
        get
        {
            if (ErrorCount > 0)
            {
                yield return ErrorCount == 1 ? "1 error" : $"{ErrorCount} errors";
            }

            if (WarningCount > 0)
            {
                yield return WarningCount == 1 ? "1 warning" : $"{WarningCount} warnings";
            }
        }
    }

    public IReadOnlyDictionary<string, string> Sources => Documents.Sources;
    public IReadOnlyList<string> SourceFiles => DocumentsSnapshot.SourceFiles;
    public string UriFor(string fileName) => Documents.UriFor(fileName);
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

    public void NotifyStatus() => StatusChanged?.Invoke();

    public void Dispose()
    {
        _compiler.StateChanged -= OnCompilerStoreChanged;
        _preferences.StateChanged -= OnPreferencesChanged;
        _compilation.StateChanged -= OnCompilationChanged;
        _documents.StateChanged -= OnDocumentsChanged;
        _workspace.StateChanged -= OnWorkspaceChanged;
        _worker.Failed -= OnWorkerFailed;
    }

    private void OnPreferencesChanged(object? sender, EventArgs e) => Notify();

    private void OnCompilationChanged(object? sender, EventArgs e) => Notify();

    private void OnDocumentsChanged(object? sender, EventArgs e) => Notify();

    private void OnWorkspaceChanged(object? sender, EventArgs e) => Notify();

    private void OnCompilerStoreChanged(object? sender, EventArgs e)
    {
        var compiler = Compiler;
        if (!string.Equals(_compilerKey, compiler.Key, StringComparison.Ordinal))
        {
            _compilerKey = compiler.Key;
            Stale = true;
        }

        if (compiler.Loading && !_compilerWasLoading)
        {
            Stale = true;
        }

        var loadingFinished = _compilerWasLoading && !compiler.Loading;
        _compilerWasLoading = compiler.Loading;
        Notify();
        if (!_suppressUrlPersist && loadingFinished)
        {
            _ = PersistUrlAsync();
        }
    }

    private async Task WaitUntilCompilerIdleAsync()
    {
        for (var i = 0; i < 2400 && Compiler.Loading; i++)
        {
            await Task.Delay(50);
        }
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            var snapshot = await _settings.LoadAsync();
            if (snapshot is not null)
            {
                _dispatcher.Dispatch(new HydratePreferencesAction(snapshot));
            }
        }
        finally
        {
            _settingsReady = true;
            _dispatcher.Dispatch(new PreferencesReadyAction());
        }

        if (_languageInit is not null)
        {
            await SetLanguageServicesAsync(LanguageServices, persist: false);
        }

        ApplySavedOutputTabs(await _settings.ReadOutputTabsAsync());
        Notify();
    }

    public async Task ReloadWorkerAsync()
    {
        WorkerError = null;
        LastInput = null;
        _liveCompiledInput = null;
        _compiledCompilerKey = null;
        Notify();
        await _worker.RecreateAsync();
        _dispatcher.Dispatch(new ResetSdkListAction());
        _languageInit = null;

        if (CompilerSpec.ToSpecifier(Sdk) is null)
        {
            _dispatcher.Dispatch(new RestoreCompilersAction(
                Sdk,
                Roslyn,
                Compiler.RoslynConfig,
                Razor,
                Compiler.RazorConfig));
        }
        else
        {
            _dispatcher.Dispatch(new ApplySdkAction(Sdk));
        }

        await WaitUntilCompilerIdleAsync();
        await InitializeLanguageServicesAsync();
    }

    private void OnWorkerFailed(string error)
    {
        WorkerError = error;
        Notify();
    }

    private LabSettingsSnapshot CaptureSettings()
    {
        var snapshot = Preferences.ToSnapshot();
        snapshot.CompilationPreferences = EditingUserPreferences
            ? GetPreferences()
            : _settings.CompilationPreferences;
        return snapshot;
    }

    private Task PersistSettingsAsync()
    {
        if (!_settingsReady)
        {
            return Task.CompletedTask;
        }

        return _settings.SaveAsync(CaptureSettings());
    }

    public Task InitializeLanguageServicesAsync()
        => _languageInit ??= SetLanguageServicesAsync(LanguageServices, persist: false);

    public Task SetLanguageServicesAsync(bool enabled)
        => SetLanguageServicesAsync(enabled, persist: true);

    private async Task SetLanguageServicesAsync(bool enabled, bool persist)
    {
        _dispatcher.Dispatch(new SetLanguageServicesAction(enabled));
        try
        {
            await _language.EnableAsync(enabled);
            if (enabled)
            {
                await SyncLanguageWorkspaceAsync(refresh: true);
                if (_liveCompiledInput is not null)
                {
                    await _language.UpdateDiagnosticsAfterCompilationAsync(Documents.UriFor(ActiveSource));
                }
                else if (Compiled is { } compiled)
                {
                    await _language.OnCachedCompilationLoadedAsync(
                        CaptureSavedState().GetCompilerConfiguration(),
                        compiled,
                        Documents.UriFor(ActiveSource));
                }
            }
            else
            {
                await _language.ApplyCompileDiagnosticsAsync(
                    Compiled,
                    Documents.SourceFiles.Select(file => (file, Documents.UriFor(file))));
            }
        }
        catch (JSException)
        {
        }

        Notify();
        if (persist)
        {
            _dispatcher.Dispatch(new PersistPreferencesAction());
        }
    }

    public Task OnSourceModelContentChangedAsync(string modelUri, ModelContentChangedEvent args)
        => _language.OnDidChangeModelContentAsync(modelUri, args);

    public Task EnableSemanticHighlightingAsync() => _language.EnableSemanticHighlightingAsync();

    public async Task OnEditorReadyAsync(string editorId, string? modelUri, bool readOnly, bool fold)
    {
        if (string.IsNullOrEmpty(modelUri))
        {
            return;
        }

        try
        {
            if (readOnly)
            {
                await _cursors.AttachOutputAsync(editorId);
                if (TryGetOutputSnapshot(DisplayOutputType, out var snapshot) &&
                    string.Equals(snapshot.ModelUri, modelUri, StringComparison.Ordinal))
                {
                    await _language.ApplyOutputEditorAsync(
                        editorId,
                        snapshot.ModelUri,
                        snapshot.Language,
                        snapshot.Metadata,
                        fold);
                    _cursors.Enable(snapshot.Metadata);
                }
            }
            else
            {
                await _cursors.AttachSourceAsync(editorId);
            }
        }
        catch (JSException)
        {
        }
    }

    public Task DetachEditorAsync(string editorId) => _cursors.DetachAsync(editorId);

    public void OnSavedStateChanged()
    {
        Stale = true;
        Notify();
        _ = PersistUrlAsync();
        if (EditingUserPreferences)
        {
            _ = PersistSettingsAsync();
        }
    }

    public void SetRazorToolchain(string value)
    {
        if (string.Equals(RazorToolchain, value, StringComparison.Ordinal))
        {
            return;
        }

        RazorToolchain = value;
        OnSavedStateChanged();
    }

    public void SetRazorStrategy(string value)
    {
        if (string.Equals(RazorStrategy, value, StringComparison.Ordinal))
        {
            return;
        }

        RazorStrategy = value;
        OnSavedStateChanged();
    }

    public IReadOnlyList<OutputTab> SettingsRowsFor(OutputFileKind kind) => Tabs.SettingsRowsFor(kind);
    public bool IsOutputTabVisible(OutputFileKind kind, string type) => Tabs.IsOutputTabVisible(kind, type);
    public bool CanMoveOutputTab(OutputFileKind kind, string type, int delta) => Tabs.CanMoveOutputTab(kind, type, delta);
    public void SetOutputTabVisible(OutputFileKind kind, string type, bool visible) => Tabs.SetOutputTabVisible(kind, type, visible);
    public void MoveOutputTab(OutputFileKind kind, string type, int delta) => Tabs.MoveOutputTab(kind, type, delta);
    public void ResetOutputTabs(OutputFileKind kind) => Tabs.ResetOutputTabs(kind);
    public string SerializeOutputTabs() => Tabs.SerializeOutputTabs();
    public void ApplySavedOutputTabs(string? json) => Tabs.ApplySavedOutputTabs(json);
    public Task PersistOutputTabsAsync() => _settings.PersistOutputTabsAsync(Tabs.SerializeOutputTabs());
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

    public Task SnapshotEditorsAsync() => InvokeHandlersAsync(SnapshotRequested);

    public async Task PersistUrlAsync(bool snapshot = false)
    {
        if (snapshot)
        {
            await SnapshotEditorsAsync();
        }

        if (_suppressUrlPersist)
        {
            return;
        }

        await InvokeHandlersAsync(UrlPersistRequested);
    }

    public SavedState CaptureSavedState()
    {
        var userFiles = Documents.SourceFiles
            .Where(file => file != LabFixtures.ConfigurationFileName)
            .ToList();

        var inputs = userFiles
            .Select(file => new InputCode
            {
                FileName = file,
                Text = Documents.Sources.GetValueOrDefault(file) ?? "",
            })
            .ToImmutableArray();

        Documents.Sources.TryGetValue(LabFixtures.ConfigurationFileName, out var configuration);

        var activeIndex = userFiles.IndexOf(ActiveSource);
        if (activeIndex < 0)
        {
            activeIndex = 0;
        }

        return new SavedState
        {
            Inputs = inputs,
            SelectedInputIndex = activeIndex,
            SelectedOutputType = ActiveOutput,
            Configuration = configuration,
            RazorToolchain = RazorToolchain switch
            {
                "Source Generator" => global::DotNetLab.RazorToolchain.SourceGenerator,
                "Internal API" => global::DotNetLab.RazorToolchain.InternalApi,
                _ => global::DotNetLab.RazorToolchain.SourceGeneratorOrInternalApi,
            },
            RazorStrategy = RazorStrategy == "DesignTime"
                ? global::DotNetLab.RazorStrategy.DesignTime
                : global::DotNetLab.RazorStrategy.Runtime,
            ShowSymbols = ShowSymbols switch
            {
                "Public Symbols" => global::DotNetLab.SymbolDisplayKinds.Public,
                "Internal Symbols" => global::DotNetLab.SymbolDisplayKinds.Internal,
                "All Symbols" => global::DotNetLab.SymbolDisplayKinds.Both,
                _ => global::DotNetLab.SymbolDisplayKinds.None,
            },
            ShowOperations = ShowOperations,
            ShowBoundNodes = ShowBoundNodes,
            ShowDeclarationDocument = ShowDeclarationDocument,
            DecodeCustomAttributeBlobs = DecodeCustomAttributeBlobs,
            ShowSequencePoints = ShowSequencePoints,
            FullIl = FullIl,
            ExcludeSingleFileNameInDiagnostics = ExcludeSingleFileNameInDiagnostics,
            IncludeHiddenDiagnostics = IncludeHiddenDiagnostics,
            SdkVersion = CompilerSpec.ToSpecifier(Sdk),
            RoslynVersion = CompilerSpec.ToSpecifier(Roslyn),
            RoslynConfiguration = CompilerSpec.ToBuildConfiguration(Compiler.RoslynConfig),
            RazorVersion = CompilerSpec.ToSpecifier(Razor),
            RazorConfiguration = CompilerSpec.ToBuildConfiguration(Compiler.RazorConfig),
        };
    }

    public async Task ApplySavedStateAsync(SavedState state)
    {
        _suppressUrlPersist = true;
        try
        {
            await ApplySavedStateCoreAsync(state);
        }
        finally
        {
            _suppressUrlPersist = false;
        }
    }

    private async Task ApplySavedStateCoreAsync(SavedState state)
    {
        if (state.Inputs.IsDefault)
        {
            state = state with { Inputs = [] };
        }

        var before = Documents.ModelUris;
        Documents.LoadFromSavedState(state);
        _ = AfterDocumentsChangedAsync(before);

        if (!string.IsNullOrEmpty(state.SelectedOutputType))
        {
            ActiveOutput = state.SelectedOutputType;
        }

        RazorToolchain = state.RazorToolchain switch
        {
            global::DotNetLab.RazorToolchain.SourceGenerator => "Source Generator",
            global::DotNetLab.RazorToolchain.InternalApi => "Internal API",
            _ => "Auto",
        };
        RazorStrategy = state.RazorStrategy == global::DotNetLab.RazorStrategy.DesignTime
            ? "DesignTime"
            : "Runtime";
        ShowSymbols = state.ShowSymbols switch
        {
            global::DotNetLab.SymbolDisplayKinds.Both => "All Symbols",
            global::DotNetLab.SymbolDisplayKinds.Internal => "Internal Symbols",
            global::DotNetLab.SymbolDisplayKinds.Public => "Public Symbols",
            _ => "No Symbols",
        };
        ShowOperations = state.ShowOperations;
        ShowBoundNodes = state.ShowBoundNodes;
        ShowDeclarationDocument = state.ShowDeclarationDocument;
        DecodeCustomAttributeBlobs = state.DecodeCustomAttributeBlobs;
        ShowSequencePoints = state.ShowSequencePoints;
        FullIl = state.FullIl;
        ExcludeSingleFileNameInDiagnostics = state.ExcludeSingleFileNameInDiagnostics;
        IncludeHiddenDiagnostics = state.IncludeHiddenDiagnostics;

        _dispatcher.Dispatch(new RestoreCompilersAction(
            CompilerSpec.Display(state.SdkVersion),
            CompilerSpec.Display(state.RoslynVersion),
            state.RoslynConfiguration == BuildConfiguration.Debug ? "Debug" : "Release",
            CompilerSpec.Display(state.RazorVersion),
            state.RazorConfiguration == BuildConfiguration.Debug ? "Debug" : "Release"));
        Stale = true;
        _liveCompiledInput = null;
        BeginNewOutputGeneration();
        Notify();

        var applyGeneration = ++_applyGeneration;
        var compilers = WaitUntilCompilerIdleAsync();

        var usedTemplateCache = TryApplyTemplateCache(state);
        if (!usedTemplateCache && EnableCaching)
        {
            _ = TryLoadServerCacheAsync(state, applyGeneration);
        }

        await compilers;

        // Template/server cache already has displayable output. Compiling again
        // (e.g. after URL/settings apply Public Symbols) reloads Tree at ~45k LOC.
        // Still run a worker compile so language services pick up #:package references.
        if (AutomaticCompilation)
        {
            // Do not block URL/state application on the compile itself — editors should
            // mount with the loaded sources rather than waiting for the worker.
            _ = CompileAsync(storeInCache: false, updateDisplayedOutput: Compiled is null);
        }
    }

    public Task ShowSettingsAsync() => SettingsRequested?.Invoke() ?? Task.CompletedTask;
    public Task ShowPaletteAsync() => PaletteRequested?.Invoke() ?? Task.CompletedTask;
    public Task ShowPasteUrlAsync() => PasteUrlRequested?.Invoke() ?? Task.CompletedTask;

    public void SetSplit(double value, bool notify = true)
    {
        var next = Math.Clamp(value, 25, 75);
        if (Math.Abs(next - Split) < 0.05)
        {
            return;
        }

        _dispatcher.Dispatch(new SetSplitAction(next));
        if (notify)
        {
            Notify();
        }
    }

    public void SetTemplate(string template)
    {
        var before = Documents.ModelUris;
        Documents.SetTemplate(template);
        _ = AfterDocumentsChangedAsync(before);
        _ = PersistUrlAsync();
    }

    public void MarkStale()
    {
        Stale = true;
        Notify();
    }

    public void SetSource(string file, string contents) => Documents.SetSource(file, contents);

    public void RenameFile(string oldName, string newName)
    {
        var before = Documents.ModelUris;
        Documents.RenameFile(oldName, newName);
        _ = AfterDocumentsChangedAsync(before);
        _ = PersistUrlAsync();
    }

    public void CloseFile(string file)
    {
        var before = Documents.ModelUris;
        Documents.CloseFile(file);
        _ = AfterDocumentsChangedAsync(before);
        _ = PersistUrlAsync();
    }

    public void AddFile(string extension)
    {
        Documents.AddFile(extension);
        _ = SyncLanguageWorkspaceAsync();
        _ = PersistUrlAsync();
    }

    public void OpenDirectives()
    {
        Documents.OpenDirectives();
        _ = SyncLanguageWorkspaceAsync();
        _ = PersistUrlAsync();
    }

    public void OpenConfiguration()
    {
        Documents.OpenConfiguration();
        _ = SyncLanguageWorkspaceAsync();
        _ = PersistUrlAsync();
    }

    public void LoadImportedFiles(IReadOnlyDictionary<string, string> files)
    {
        var before = Documents.ModelUris;
        Documents.LoadImportedFiles(files);
        _ = AfterDocumentsChangedAsync(before);
        _ = PersistUrlAsync();
    }

    public async Task FormatActiveSource()
    {
        var fileName = ActiveSource;
        if (!Documents.Sources.TryGetValue(fileName, out var currentCode))
        {
            return;
        }

        if (!fileName.IsCSharpFileName(out var isScript) &&
            fileName != LabFixtures.ConfigurationFileName)
        {
            return;
        }

        try
        {
            var formatted = await _worker.SendAsync(
                new WorkerInputMessage.FormatCode(currentCode, isScript)
                {
                    Id = _worker.NextMessageId(),
                });

            if (formatted == currentCode)
            {
                return;
            }

            Documents.SetSource(fileName, formatted);
            await SyncLanguageWorkspaceAsync();
            await PersistUrlAsync();
        }
        catch
        {
            // Same as Lab: formatting is best-effort and should not interrupt editing.
        }
    }

    public void SetActiveSource(string file)
    {
        Documents.SetActiveSource(file);
        _ = SyncLanguageWorkspaceAsync();
        RefreshTemporaryErrorList();
        _ = LoadDisplayedOutputAsync();
        _ = PersistUrlAsync();
    }

    public Task CompileAsync() => CompileAsync(storeInCache: true);

    public Task CompileAsync(bool storeInCache) => CompileAsync(storeInCache, updateDisplayedOutput: true);

    public async Task CompileAsync(bool storeInCache, bool updateDisplayedOutput)
    {
        if (Running || CompilerLoading)
        {
            return;
        }

        var input = CreateCompilationInput();
        if (CanReuseLastCompile(input))
        {
            Stale = false;
            Notify();
            await PersistUrlAsync(snapshot: true);
            if (storeInCache && Compiled is { } reused)
            {
                TryStoreInCache(CaptureSavedState(), reused);
            }

            if (updateDisplayedOutput && _outputCache.Count == 0)
            {
                _ = LoadDisplayedOutputAsync();
            }

            _ = RefreshLanguageServicesAfterCompileAsync();
            return;
        }

        var showBusy = storeInCache || (updateDisplayedOutput && Compiled is null);
        var appliedToDisplay = false;
        if (showBusy)
        {
            Running = true;
            Notify();
            // Cached same-input compiles can finish synchronously; yield so Busy UI can paint.
            await Task.Yield();
        }

        try
        {
            if (showBusy)
            {
                await PersistUrlAsync(snapshot: true);
            }

            LastInput = input;
            var compiled = await _worker.SendAsync(
                new WorkerInputMessage.Compile(input, LanguageServicesEnabled: LanguageServices)
                {
                    Id = _worker.NextMessageId(),
                });
            _liveCompiledInput = input;
            _compiledCompilerKey = CompilerKey();

            // Keep template/server-cache Tree on screen after refresh. A live compile
            // is still required so language services get #:package references.
            var applyToDisplay = storeInCache || (updateDisplayedOutput && Compiled is null);
            if (applyToDisplay)
            {
                var sameAssembly = ReferenceEquals(Compiled, compiled);
                Compiled = compiled;
                _storeInCache = storeInCache;
                Stale = false;
                appliedToDisplay = true;
                // The worker reuses LastResult for identical input. Keep the output cache so
                // the editor is not forced through Compiling/Loading for an unchanged assembly.
                if (!sameAssembly)
                {
                    BeginNewOutputGeneration();
                }

                if (storeInCache)
                {
                    TryStoreInCache(CaptureSavedState(), compiled);
                }
            }
        }
        catch (Exception ex)
        {
            if (storeInCache || (updateDisplayedOutput && Compiled is null))
            {
                Compiled = CompiledAssembly.Fail(ex.ToString());
                LastInput = input;
                _liveCompiledInput = input;
                _compiledCompilerKey = CompilerKey();
                BeginNewOutputGeneration();
                appliedToDisplay = true;
            }
            else
            {
                _logger.LogError(ex, "Language services compile after cached output failed.");
            }
        }
        finally
        {
            if (showBusy)
            {
                Running = false;
                Notify();
            }
        }

        if (appliedToDisplay)
        {
            RefreshTemporaryErrorList();
            _ = LoadDisplayedOutputAsync();
        }

        await RefreshLanguageServicesAfterCompileAsync();
    }

    private bool CanReuseLastCompile(CompilationInput input)
        => _liveCompiledInput is { } live
           && live.Equals(input)
           && string.Equals(_compiledCompilerKey, CompilerKey(), StringComparison.Ordinal);

    private string CompilerKey() => Compiler.Key;

    private CompilerState Compiler => _compiler.Value;

    private PreferencesState Preferences => _preferences.Value;

    private CompilationState Compilation => _compilation.Value;

    private DocumentsState DocumentsSnapshot => _documents.Value;

    private WorkspaceState WorkspaceSnapshot => _workspace.Value;

    private void BeginNewOutputGeneration()
    {
        _compileGeneration++;
        _outputCache.Clear();
        _outputLoading.Clear();
    }

    public CompilationInput CreateCompilationInput()
    {
        var inputs = Documents.SourceFiles
            .Where(file => file != LabFixtures.ConfigurationFileName)
            .Select(file => new InputCode
            {
                FileName = file,
                Text = Documents.Sources.GetValueOrDefault(file) ?? "",
            })
            .ToImmutableArray();

        Documents.Sources.TryGetValue(LabFixtures.ConfigurationFileName, out var configuration);

        return new CompilationInput(inputs)
        {
            Configuration = configuration,
            RazorToolchain = this.RazorToolchain switch
            {
                "Source Generator" => global::DotNetLab.RazorToolchain.SourceGenerator,
                "Internal API" => global::DotNetLab.RazorToolchain.InternalApi,
                _ => global::DotNetLab.RazorToolchain.SourceGeneratorOrInternalApi,
            },
            RazorStrategy = this.RazorStrategy == "DesignTime"
                ? global::DotNetLab.RazorStrategy.DesignTime
                : global::DotNetLab.RazorStrategy.Runtime,
            Preferences = GetPreferences(),
        };
    }

    public CompilationPreferences GetPreferences()
        => new()
        {
            ShowSymbolKinds = ShowSymbols switch
            {
                "Public Symbols" => global::DotNetLab.SymbolDisplayKinds.Public,
                "Internal Symbols" => global::DotNetLab.SymbolDisplayKinds.Internal,
                "All Symbols" => global::DotNetLab.SymbolDisplayKinds.Both,
                _ => global::DotNetLab.SymbolDisplayKinds.None,
            },
            ShowOperations = ShowOperations,
            ShowBoundNodes = ShowBoundNodes,
            ShowDeclarationDocument = ShowDeclarationDocument,
            DecodeCustomAttributeBlobs = DecodeCustomAttributeBlobs,
            ShowSequencePoints = ShowSequencePoints,
            FullIl = FullIl,
            ExcludeSingleFileNameInDiagnostics = ExcludeSingleFileNameInDiagnostics,
            IncludeHiddenDiagnostics = IncludeHiddenDiagnostics,
        };

    public string OutputLanguage(string type)
        => TryGetOutputSnapshot(type, out var snapshot)
            ? snapshot.Language
            : "plaintext";

    public string OutputUriFor(string tab)
    {
        if (!_outputModelUris.TryGetValue(tab, out var uri))
        {
            uri = CompiledAssembly.GetOutputModelUri(OutputFileName(tab), tab);
            _outputModelUris[tab] = uri;
        }

        return uri;
    }

    public string LanguageFor(string fileName) => LabCatalog.LanguageFor(fileName);

    public string GetOutput(string tab)
    {
        var key = OutputCacheKey(tab);
        if (_outputCache.TryGetValue(key, out var cached))
        {
            return cached.Text;
        }

        if (Running)
        {
            // Keep the previous output in Monaco. Replacing it with "Compiling…" races
            // with a cached recompile and can leave that placeholder stuck until a tab switch.
            var runningOutput = FindOutput(tab);
            if (runningOutput?.Text is { } runningText)
            {
                return runningText;
            }

            return "Compiling…";
        }

        if (Compiled is not { } compiled)
        {
            return "(press Compile to load this)";
        }

        if (compiled.GetGlobalOutput("fail") is { Text: { } failText })
        {
            return failText;
        }

        var output = FindOutput(tab);
        if (output is null)
        {
            return $"(no {OutputLabel(tab)} output for this file)";
        }

        if (output.Text is { } eager)
        {
            _outputCache[key] = CreateSnapshot(tab, eager, output, output.Metadata);
            return eager;
        }

        _ = EnsureOutputLoadedAsync(tab);
        return "Loading…";
    }

    public async Task EnsureOutputLoadedAsync(string tab)
    {
        if (Compiled is null || LastInput is null || Running)
        {
            return;
        }

        var key = OutputCacheKey(tab);
        if (_outputCache.ContainsKey(key) || !_outputLoading.Add(key))
        {
            return;
        }

        var generation = _compileGeneration;
        try
        {
            // LoadAsync is often already completed for a cached assembly. Yield so Notify
            // does not run in the middle of a Blazor render (GetOutput is called from one).
            await Task.Yield();
            if (generation != _compileGeneration || Running || Compiled is null)
            {
                return;
            }

            var output = FindOutput(tab);
            if (output is null)
            {
                _outputCache[key] = Placeholder(tab, $"(no {OutputLabel(tab)} output for this file)");
                return;
            }

            if (output.Text is { } eager)
            {
                _outputCache[key] = CreateSnapshot(tab, eager, output, output.Metadata);
                return;
            }

            var file = OutputFileName(tab);
            CompiledFileLazyResult result;
            try
            {
                result = await output.LoadAsync(new()
                {
                    OutputFactory = () => LoadOutputFromWorkerAsync(file, tab),
                });
            }
            catch (Exception ex)
            {
                result = new() { Text = ex.ToString(), Metadata = CompiledFileOutputMetadata.SpecialMessage };
            }

            if (generation != _compileGeneration)
            {
                return;
            }

            _outputCache[key] = CreateSnapshot(tab, result.Text, output, result.Metadata ?? output.Metadata);
            if (_storeInCache && Compiled is { } compiled)
            {
                TryStoreInCache(CaptureSavedState(), compiled);
            }
        }
        finally
        {
            _outputLoading.Remove(key);
            Notify();
        }
    }

    private void RefreshTemporaryErrorList()
    {
        Tabs.EnsureActiveOutput();
        _showErrorListIfOutputEmpty = Compiled is { NumErrors: > 0 };
        Notify();
    }

    private async Task LoadDisplayedOutputAsync()
    {
        await EnsureOutputLoadedAsync(_activeOutput);
        if (!string.Equals(DisplayOutputType, _activeOutput, StringComparison.Ordinal))
        {
            await EnsureOutputLoadedAsync(DisplayOutputType);
        }
    }

    private bool? HasEmptyOutputText(string tab)
    {
        var output = FindOutput(tab);
        if (output is null)
        {
            return null;
        }

        if (output.Text is { } eager)
        {
            return string.IsNullOrEmpty(eager);
        }

        if (_outputCache.TryGetValue(OutputCacheKey(tab), out var snapshot))
        {
            return string.IsNullOrEmpty(snapshot.Text);
        }

        return null;
    }

    private async ValueTask<CompiledFileLazyResult> LoadOutputFromWorkerAsync(string? file, string tab)
    {
        return await _worker.SendAsync(
            new WorkerInputMessage.GetOutput(LastInput!, file, tab)
            {
                Id = _worker.NextMessageId(),
            });
    }

    private CompiledFileOutput? FindOutput(string tab)
    {
        if (Compiled is not { } compiled)
        {
            return null;
        }

        if (compiled.Files.TryGetValue(ActiveSource, out var file) &&
            file.GetOutput(tab) is { } perFile)
        {
            return perFile;
        }

        return compiled.GetGlobalOutput(tab);
    }

    private string? OutputFileName(string tab)
        => FindOutput(tab) is not null &&
           Compiled?.Files.TryGetValue(ActiveSource, out var file) == true &&
           file.GetOutput(tab) is not null
            ? ActiveSource
            : null;

    private string OutputCacheKey(string tab) => $"{ActiveSource}\0{tab}";

    private bool TryGetOutputSnapshot(string tab, out OutputSnapshot snapshot)
        => _outputCache.TryGetValue(OutputCacheKey(tab), out snapshot!);

    private OutputSnapshot Placeholder(string tab, string text)
        => new(text, "plaintext", CompiledFileOutputMetadata.SpecialMessage, OutputUriFor(tab));

    private OutputSnapshot CreateSnapshot(
        string tab,
        string text,
        CompiledFileOutput? output,
        CompiledFileOutputMetadata? metadata)
    {
        var language = metadata is { MessageKind: not MessageKind.Normal }
            ? "plaintext"
            : output?.Language ?? LabCatalog.OutputLanguage(tab);
        return new(text, language, metadata, OutputUriFor(tab));
    }

    private sealed record OutputSnapshot(
        string Text,
        string Language,
        CompiledFileOutputMetadata? Metadata,
        string ModelUri);

    private async Task AfterDocumentsChangedAsync(IReadOnlyList<string> before)
    {
        var removed = before.Except(Documents.ModelUris).ToArray();
        await SyncLanguageWorkspaceAsync(refresh: true, removed);
    }

    private async Task SyncLanguageWorkspaceAsync(bool refresh = false, IReadOnlyList<string>? disposeUris = null)
    {
        if (disposeUris is { Count: > 0 })
        {
            foreach (var uri in disposeUris)
            {
                await _language.DisposeModelAsync(uri);
            }
        }

        try
        {
            await _language.OnDidChangeWorkspaceAsync(
                Documents.CreateModelInfos(),
                Documents.UriFor(ActiveSource),
                refresh);
        }
        catch (JSException)
        {
        }
    }

    private async Task RefreshLanguageServicesAfterCompileAsync()
    {
        var uri = Documents.UriFor(ActiveSource);
        if (!_language.Enabled || !await _language.UpdateDiagnosticsAfterCompilationAsync(uri))
        {
            await _language.ApplyCompileDiagnosticsAsync(
                Compiled,
                Documents.SourceFiles.Select(file => (file, Documents.UriFor(file))));
        }

        if (_language.Enabled)
        {
            await SyncLanguageWorkspaceAsync(refresh: true);
        }
    }

    private bool TryApplyTemplateCache(SavedState state)
    {
        try
        {
            var preferencesDiffer = state.GetPreferences() != CompilationPreferences.Default;
            if (TryGetTemplateOutput(state, out var input, out var output))
            {
                ApplyCachedCompilation(input, output, stale: preferencesDiffer);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply template cache.");
        }

        return false;
    }

    private bool TryGetTemplateOutput(
        SavedState state,
        [NotNullWhen(true)] out CompilationInput? input,
        [NotNullWhen(true)] out CompiledAssembly? output)
    {
        var lookup = state.WithPreferences(CompilationPreferences.Default);
        if (_templates.TryGetOutput(lookup, out input, out output) && output is not null)
        {
            return true;
        }

        foreach (var wellKnown in (ReadOnlySpan<SavedState>)[SavedState.CSharp, SavedState.Razor, SavedState.Cshtml])
        {
            if (!SourcesEqual(lookup, wellKnown))
            {
                continue;
            }

            if (_templates.TryGetOutput(wellKnown, out input, out output) && output is not null)
            {
                return true;
            }
        }

        input = null;
        output = null;
        return false;
    }

    private static bool SourcesEqual(SavedState left, SavedState right)
    {
        if (left.Inputs.IsDefault || right.Inputs.IsDefault || left.Inputs.Length != right.Inputs.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Inputs.Length; i++)
        {
            if (!string.Equals(left.Inputs[i].FileName, right.Inputs[i].FileName, StringComparison.Ordinal) ||
                !string.Equals(left.Inputs[i].Text, right.Inputs[i].Text, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private async Task TryLoadServerCacheAsync(SavedState state, int applyGeneration)
    {
        var result = await _cache.LoadAsync(state);
        if (applyGeneration != _applyGeneration || result is null)
        {
            return;
        }

        var (output, _) = result.Value;

        var input = state.ToCompilationInput();
        if (_liveCompiledInput is { } live && live.Equals(input))
        {
            return;
        }

        ApplyCachedCompilation(input, output, stale: false);
    }

    private void ApplyCachedCompilation(CompilationInput input, CompiledAssembly output, bool stale)
    {
        if (_liveCompiledInput is { } live && live.Equals(input))
        {
            return;
        }

        LastInput = input;
        Compiled = output;
        _compiledCompilerKey = CompilerKey();
        Stale = stale;
        BeginNewOutputGeneration();
        RefreshTemporaryErrorList();
        Notify();
        _ = LoadDisplayedOutputAsync();
        _ = RefreshLanguageServicesAfterCachedCompileAsync(output);
    }

    private void TryStoreInCache(SavedState state, CompiledAssembly output)
    {
        if (!EnableCaching || _templates.HasInput(state))
        {
            return;
        }

        _ = _cache.StoreAsync(state, output);
    }

    private async Task RefreshLanguageServicesAfterCachedCompileAsync(CompiledAssembly output)
    {
        var uri = Documents.UriFor(ActiveSource);
        var config = CaptureSavedState().GetCompilerConfiguration();
        if (!_language.Enabled || !await _language.OnCachedCompilationLoadedAsync(config, output, uri))
        {
            await _language.ApplyCompileDiagnosticsAsync(
                Compiled,
                Documents.SourceFiles.Select(file => (file, Documents.UriFor(file))));
        }

        if (_language.Enabled)
        {
            await SyncLanguageWorkspaceAsync(refresh: true);
        }
    }

    private static async Task InvokeHandlersAsync(Func<Task>? handlers)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            await ((Func<Task>)handler)();
        }
    }
}
