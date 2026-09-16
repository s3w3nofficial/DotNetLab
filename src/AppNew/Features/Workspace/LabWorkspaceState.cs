using System.Collections.Immutable;
using BlazorMonaco.Editor;
using DotNetLab.Editor.LanguageServices;
using DotNetLab.Features.Compiler;
using DotNetLab.Features.Compilation;
using DotNetLab.Features.Documents;
using DotNetLab.Features.Outputs;
using DotNetLab.Features.Preferences;
using DotNetLab.Infrastructure.Persistence;
using DotNetLab.Infrastructure.Worker;
using DotNetLab.Lab;
using Fluxor;
using Microsoft.JSInterop;

namespace DotNetLab.Features.Workspace;

public sealed class LabWorkspaceState : IDocumentWorkspace, IOutputWorkspace, IOutputLoadHost, ICompilationWorkspace, IDisposable
{
    private readonly WorkerHost _worker;
    private readonly LabLanguageServices _language;
    private readonly LabCursorSync _cursors;
    private readonly LabSettings _settings;
    private readonly IState<CompilerState> _compiler;
    private readonly IState<PreferencesState> _preferences;
    private readonly IState<CompilationState> _compilation;
    private readonly IState<DocumentsState> _documents;
    private readonly IState<WorkspaceState> _workspace;
    private readonly IState<OutputsState> _outputs;
    private readonly IDispatcher _dispatcher;
    private bool _suppressUrlPersist;
    private bool _settingsReady;
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
        IState<OutputsState> outputs,
        IDispatcher dispatcher,
        ILogger<LabWorkspaceState> logger)
    {
        _worker = worker;
        _language = language;
        _cursors = cursors;
        _settings = settings;
        _compiler = compiler;
        _preferences = preferences;
        _compilation = compilation;
        _documents = documents;
        _workspace = workspace;
        _outputs = outputs;
        _dispatcher = dispatcher;
        Documents = new LabDocuments(this, dispatcher);
        Tabs = new OutputTabLayout(this);
        OutputCache = new OutputLoadCache(this);
        Compilation = new CompilationSession(
            this,
            worker,
            templates,
            cache,
            compiler,
            preferences,
            compilation,
            dispatcher,
            logger);
        PublishOutputs();
        _compiler.StateChanged += OnCompilerStoreChanged;
        _preferences.StateChanged += OnPreferencesChanged;
        _compilation.StateChanged += OnCompilationChanged;
        _documents.StateChanged += OnDocumentsChanged;
        _workspace.StateChanged += OnWorkspaceChanged;
        _outputs.StateChanged += OnOutputsChanged;
        _worker.Failed += OnWorkerFailed;
    }

    public LabDocuments Documents { get; }
    public OutputTabLayout Tabs { get; }
    public OutputLoadCache OutputCache { get; }
    public CompilationSession Compilation { get; }

    public event Action? Changed;
    public event Action? StatusChanged;
    public event Func<Task>? SettingsRequested;
    public event Func<Task>? PaletteRequested;
    public event Func<Task>? PasteUrlRequested;
    public event Func<Task>? SnapshotRequested;
    public event Func<Task>? UrlPersistRequested;

    public bool Running => _compilation.Value.Running;
    public bool Stale
    {
        get => _compilation.Value.Stale;
        set => _dispatcher.Dispatch(new SetStaleAction(value));
    }

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
        get => OutputsSnapshot.ActiveOutput;
        set
        {
            var dismiss = OutputCache.DismissTemporaryErrorList();
            if (string.Equals(OutputsSnapshot.ActiveOutput, value, StringComparison.Ordinal))
            {
                if (dismiss)
                {
                    Notify();
                }

                return;
            }

            PublishOutputs(value);
            _ = OutputCache.EnsureOutputLoadedAsync(value);
            _ = PersistUrlAsync();
        }
    }

    public string RazorToolchain { get; set; } = "Auto";
    public string RazorStrategy { get; set; } = "Runtime";
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
    public string? WorkerError { get; private set; }
    public bool EditingUserPreferences { get; set; }

    public void Notify() => Changed?.Invoke();

    public void NotifyStatus() => StatusChanged?.Invoke();

    public void Dispose()
    {
        _compiler.StateChanged -= OnCompilerStoreChanged;
        _preferences.StateChanged -= OnPreferencesChanged;
        _compilation.StateChanged -= OnCompilationChanged;
        _documents.StateChanged -= OnDocumentsChanged;
        _workspace.StateChanged -= OnWorkspaceChanged;
        _outputs.StateChanged -= OnOutputsChanged;
        _worker.Failed -= OnWorkerFailed;
    }

    private void OnPreferencesChanged(object? sender, EventArgs e) => Notify();

    private void OnCompilationChanged(object? sender, EventArgs e) => Notify();

    private void OnDocumentsChanged(object? sender, EventArgs e) => Notify();

    private void OnWorkspaceChanged(object? sender, EventArgs e) => Notify();

    private void OnOutputsChanged(object? sender, EventArgs e) => Notify();

    private void OnCompilerStoreChanged(object? sender, EventArgs e)
    {
        var compiler = Compiler;
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
            await SetLanguageServicesAsync(Preferences.LanguageServices, persist: false);
        }

        Tabs.ApplySavedOutputTabs(await _settings.ReadOutputTabsAsync());
        Notify();
    }

    public async Task ReloadWorkerAsync()
    {
        WorkerError = null;
        Compilation.ResetWorkerState();
        Notify();
        await _worker.RecreateAsync();
        _dispatcher.Dispatch(new ResetSdkListAction());
        _languageInit = null;

        if (CompilerSpec.ToSpecifier(Compiler.Sdk) is null)
        {
            _dispatcher.Dispatch(new RestoreCompilersAction(
                Compiler.Sdk,
                Compiler.Roslyn,
                Compiler.RoslynConfig,
                Compiler.Razor,
                Compiler.RazorConfig));
        }
        else
        {
            _dispatcher.Dispatch(new ApplySdkAction(Compiler.Sdk));
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
        => _languageInit ??= SetLanguageServicesAsync(Preferences.LanguageServices, persist: false);

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
                if (Compilation.HasLiveInput)
                {
                    await _language.UpdateDiagnosticsAfterCompilationAsync(Documents.UriFor(ActiveSource));
                }
                else if (Compilation.Compiled is { } compiled)
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
                    Compilation.Compiled,
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
                if (OutputCache.TryGetSnapshot(OutputCache.DisplayType, out var snapshot) &&
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

    public Task PersistOutputTabsAsync() => _settings.PersistOutputTabsAsync(Tabs.SerializeOutputTabs());

    public void EnsureActiveOutput() => Tabs.EnsureActiveOutput();

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
            SdkVersion = CompilerSpec.ToSpecifier(Compiler.Sdk),
            RoslynVersion = CompilerSpec.ToSpecifier(Compiler.Roslyn),
            RoslynConfiguration = CompilerSpec.ToBuildConfiguration(Compiler.RoslynConfig),
            RazorVersion = CompilerSpec.ToSpecifier(Compiler.Razor),
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
        var applyGeneration = Compilation.InvalidateForNewState();
        Notify();

        var compilers = WaitUntilCompilerIdleAsync();

        var usedTemplateCache = Compilation.TryApplyTemplateCache(state);
        if (!usedTemplateCache && Preferences.EnableCaching)
        {
            _ = Compilation.TryLoadServerCacheAsync(state, applyGeneration);
        }

        await compilers;

        if (Preferences.AutomaticCompilation)
        {
            _ = Compilation.CompileAsync(storeInCache: false, updateDisplayedOutput: Compilation.Compiled is null);
        }
    }

    public Task ShowSettingsAsync() => SettingsRequested?.Invoke() ?? Task.CompletedTask;
    public Task ShowPaletteAsync() => PaletteRequested?.Invoke() ?? Task.CompletedTask;
    public Task ShowPasteUrlAsync() => PasteUrlRequested?.Invoke() ?? Task.CompletedTask;

    public void SetSplit(double value, bool notify = true)
    {
        var next = Math.Clamp(value, 25, 75);
        if (Math.Abs(next - WorkspaceSnapshot.Split) < 0.05)
        {
            return;
        }

        _dispatcher.Dispatch(new SetSplitAction(next));
        if (notify)
        {
            Notify();
        }
    }

    public void MarkStale()
    {
        Stale = true;
        Notify();
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

    private CompilerState Compiler => _compiler.Value;

    private PreferencesState Preferences => _preferences.Value;

    private DocumentsState DocumentsSnapshot => _documents.Value;

    private WorkspaceState WorkspaceSnapshot => _workspace.Value;

    private OutputsState OutputsSnapshot => _outputs.Value;

    public void PublishOutputs(string? activeOutput = null)
    {
        _dispatcher.Dispatch(new SetOutputsAction(new OutputsState
        {
            ActiveOutput = activeOutput ?? OutputsSnapshot.ActiveOutput,
            Revision = Tabs.Revision,
            CurrentOutputTabIds = [.. Tabs.CurrentOutputTabIds],
        }));
    }

    void IOutputWorkspace.PublishOutputs() => PublishOutputs();

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

    CompiledAssembly? IOutputLoadHost.Compiled => Compilation.Compiled;

    CompilationInput? IOutputLoadHost.LastInput => Compilation.LastInput;

    int IOutputLoadHost.CompileGeneration => Compilation.CompileGeneration;

    bool IOutputLoadHost.IsCurrentCompile(int generation) => Compilation.IsCurrentCompile(generation);

    bool IOutputLoadHost.StoreInCache => Compilation.StoreInCache;

    string IOutputLoadHost.OutputLabel(string tab) => Tabs.OutputLabel(tab);

    ValueTask<CompiledFileLazyResult> IOutputLoadHost.LoadFromWorkerAsync(string? file, string tab)
        => LoadOutputFromWorkerAsync(file, tab);

    void IOutputLoadHost.StoreCompiledOutput(CompiledAssembly compiled)
        => Compilation.StoreCompiledOutput(compiled);

    private async ValueTask<CompiledFileLazyResult> LoadOutputFromWorkerAsync(string? file, string tab)
    {
        return await _worker.SendAsync(
            new WorkerInputMessage.GetOutput(Compilation.LastInput!, file, tab)
            {
                Id = _worker.NextMessageId(),
            });
    }

    public async Task AfterDocumentsChangedAsync(IReadOnlyList<string> before)
    {
        var removed = before.Except(Documents.ModelUris).ToArray();
        await SyncLanguageWorkspaceAsync(refresh: true, removed);
    }

    void IDocumentWorkspace.AfterActiveSourceChanged()
    {
        _ = SyncLanguageWorkspaceAsync();
        Compilation.RefreshTemporaryErrorList();
        _ = OutputCache.LoadDisplayedAsync();
        _ = PersistUrlAsync();
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

    public async Task RefreshLanguageServicesAfterCompileAsync()
    {
        var uri = Documents.UriFor(ActiveSource);
        if (!_language.Enabled || !await _language.UpdateDiagnosticsAfterCompilationAsync(uri))
        {
            await _language.ApplyCompileDiagnosticsAsync(
                Compilation.Compiled,
                Documents.SourceFiles.Select(file => (file, Documents.UriFor(file))));
        }

        if (_language.Enabled)
        {
            await SyncLanguageWorkspaceAsync(refresh: true);
        }
    }

    public async Task RefreshLanguageServicesAfterCachedCompileAsync(CompiledAssembly output)
    {
        var uri = Documents.UriFor(ActiveSource);
        var config = CaptureSavedState().GetCompilerConfiguration();
        if (!_language.Enabled || !await _language.OnCachedCompilationLoadedAsync(config, output, uri))
        {
            await _language.ApplyCompileDiagnosticsAsync(
                Compilation.Compiled,
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
