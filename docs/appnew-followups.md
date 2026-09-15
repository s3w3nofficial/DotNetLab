# AppNew follow-ups

Track remaining work after the UI folder split and the first ISP cuts
(`ILabStatus`, `ILabBrand`, `ILabCommands`, `ILabPalette`, `ILabSettings`,
`ILabShell`). AppNew stays a WASM rewrite behind `WebAssemblyNew` until
host-neutral `src/App` replacement is an explicit goal.

Keep the current chrome folders until feature stores exist, then move into
[Target folders](#target-folders). `LabWorkspace` still injects
`LabWorkspaceState`. Do not Fluxor the current god object; see
[State direction](#state-direction).

## Done

- [x] Folder-split chrome and editor shell (Header, Settings, Dialogs, StatusBar, Workspace)
- [x] `StatusBar` injects `ILabStatus`
- [x] `LabBrandBar` injects `ILabBrand`
- [x] `LabCommandBar` injects `ILabCommands`
- [x] Fluxor store + first feature (`Features/Updates`), not wrapping `LabWorkspaceState`
- [x] `CommandPalette` injects `ILabPalette`
- [x] `SettingsDialog` injects `ILabSettings`
- [x] `MainLayout` injects `ILabShell`
- [x] `ILabEnvironment` instead of `IWebAssemblyHostEnvironment` on workspace / worker
- [x] Settings identity from git commit / date
- [x] Splitter pointer-move in JS; `LabCodeEditor` `IAsyncDisposable`
- [x] `CompilerStore` under `Lab/` (`CompilerState`; worker apply stays on the workspace)
- [x] Fluxor `Features/Compiler` with `CompilerPicker` / `CompilerSection` / `CompilerSettings`
- [x] `PreferencesStore` under `Lab/` (`PreferencesState`; persist / language-services apply stay on the workspace)
- [x] Fluxor `Features/Preferences` with `PreferenceSettings`; persist in effects, language-services apply on the workspace
- [x] `CompilationStore` under `Lab/` (`CompilationState`; worker compile and `CompiledAssembly` stay on the workspace)
- [x] Fluxor `Features/Compilation` (`Running` / `Stale`; worker compile stays on the workspace)
- [x] `StatusSelectors` (`CompilationState` + `CompilerState` → right pill / ready; `DocumentsState` → template / active file; cursor and diagnostics stay on `ILabStatus`)
- [x] `DocumentsStore` under `Lab/` (`DocumentsState`; file contents stay on `LabDocuments`; Monaco stays source of truth)
- [x] Fluxor `Features/Documents` (template / active file / file list / URIs; file contents stay on `LabDocuments`)

## P0

- [x] Render the shell even if restore fails
  - Wrap theme / platform / settings / URL load in `MainLayout`
  - Set `_ready` in `finally` so a throw cannot leave a blank page
  - Assign `_appliedSlug` only after a successful apply
- [x] Sandbox the HTML preview iframe (`sandbox=""` on `srcdoc`)

## P1

- [x] Replace `EditorGroups` static drag fields with a scoped `EditorDragState`
- [x] Fix `LabLanguageServices` `_outputRegistered` so a failed JS register can retry
- [x] Continue ISP: `CommandPalette` off `LabWorkspaceState`
- [x] Continue ISP: `SettingsDialog` off `LabWorkspaceState`
- [x] Continue ISP: `MainLayout` off `LabWorkspaceState`

## P2

- [x] Introduce `ILabEnvironment` and drop `IWebAssemblyHostEnvironment` from `LabWorkspaceState`
- [x] Make `WorkerHost` `IAsyncDisposable` and protect recreate vs in-flight work
- [x] Expose `Sources` / `SourceFiles` as `IReadOnly*` and mutate through commands
- [x] Move output-tab persistence out of Razor (`MainLayout`, `LabWorkspace`, `SettingsDialog`)

## P3

- [x] Generate commit / date instead of hardcoded Settings identity (`3f19ab2`)
- [x] Move splitter pointer-move hot path to JS; persist final `Split` only
- [x] Make `LabCodeEditor` `IAsyncDisposable` so teardown cannot race subscriptions

## State direction

`ILab*` on `LabWorkspaceState` is a facade, not ownership. The goal is
feature-owned immutable state, with runtime resources kept as ordinary services.

```
DocumentsStore     CompilerStore     LayoutStore     PreferencesStore
        \                |                 |                /
         \               |                 |               /
          v              v                 v              v
                    (selectors: status, etc.)
                              |
                    Razor components
```

Runtime, not state: `WorkerHost`, Monaco instances / models / subscriptions,
`CancellationTokenSource`, `JSObjectReference`, timers, Roslyn objects,
`CompiledAssembly`.

Monaco stays the source of truth for buffer text. Compile / share / URL persist
read the editor; do not dispatch on every keystroke. Document state is file
list, active file, and URIs.

Status is derived (`Compilation` + `Compiler` + `Documents` → selector), not a
writable `ILabStatus` store. Keep `ILabStatus` for cursor and diagnostics until
those have stores.

Do **not** replace `LabWorkspaceState` with one `AppState` record. Do **not**
delete the facade in one pass.

### Sequence

1. Fluxor in the host (`AddFluxor`, `StoreInitializer`). First feature is
   **Updates** (`Features/Updates`): `IUpdateChecker` stays infrastructure;
   `LoadUpdate` stays on the checker (not serializable). `LabBrandBar` /
   Settings check UI read `IState<UpdateState>`.
2. Remaining P0 bugs (shell always renders, slug after apply, HTML sandbox).
3. P1 chrome ISP (`CommandPalette`, `SettingsDialog`, `MainLayout`).
4. Extract small scoped stores (`CompilationStore`, `CompilerStore`,
   `PreferencesStore`, …) next to the current types in `Lab/`.
   `StateStore<T>` / immutable records / generation checks are enough until a
   slice is ready to become a Fluxor feature.
5. When a store is real, colocate its UI with it (e.g. `CompilerPicker` +
   `CompilerSection` move with `CompilerStore`, not before) and optionally
   convert that slice to Fluxor the same way Updates was converted.
6. Shrink `LabWorkspaceState` / `Lab/` until both disappear.
7. Cosmetic leftover: `Header/` → `Shell/Header/` for brand / command / memory
   only.

Fluxor constraints: no keystrokes, no Monaco handles, no worker handles, no
`CompiledAssembly` in the store. Effects for async; reducers for
`{ Running, Stale, SelectedSdk, … }`. Do not create empty
`Features/*/…Actions.cs` ahead of a store. Do not wrap `LabWorkspaceState`.

## Target folders

Adopt a feature-oriented layout. Current Header / Settings / Dialogs /
StatusBar / Workspace / Monaco / Lab match *where things render*. The target
matches *what changes together*.

```
AppNew/
├── Pages/                    Home, NotFound
├── Layout/                   MainLayout only (LayoutComponentBase)
├── Features/
│   ├── Documents/            state, documents UI, templates/fixtures
│   ├── Compiler/             state, CompilerPicker, CompilerSettings, catalog
│   ├── Compilation/          compile session state (not WorkerHost)
│   ├── Outputs/              output tabs/view, OutputTabLayout
│   ├── Workspace/            LabWorkspace, EditorGroups, splitter
│   ├── Preferences/          SettingsDialog as composer, SettingRow
│   ├── Sharing/              URL/gist, PasteUrlDialog, LabShare, LabUrlSync
│   ├── Theme/
│   └── Updates/
├── Shell/
│   ├── Header/               LabBrandBar, LabCommandBar, MemoryUsageView
│   ├── StatusBar/            StatusBar + StatusSelectors
│   └── CommandPalette/
├── Editor/
│   ├── Components/           LabCodeEditor
│   ├── Monaco/               interop, markers, language providers
│   └── LanguageServices/     LabLanguageServices, cursor sync
├── Infrastructure/
│   ├── Worker/               WorkerHost
│   ├── Browser/              LabPlatform, IScreenInfo
│   ├── Persistence/          InputOutputCache, TemplateCache
│   └── Logging/              LabLogging
├── Shared/
└── wwwroot/
```

Three kinds of code:

| Kind | Owns | Examples |
|---|---|---|
| `Features/` | What the lab does | compiler selection, documents, outputs, sharing |
| `Infrastructure/` | How the runtime does it | worker, caches, platform, logging |
| `Editor/` | Monaco adapter | models, providers, `LabCodeEditor` |

`SettingsDialog` composes `<CompilerSettings />`, `<ThemeSettings />`, etc. It
does not own every setting. `CompilerPicker` lives with Compiler even if the
header renders it. Status is
`CompilationState` + `CompilerState` + `DocumentsState` → `StatusBar/StatusSelectors.cs`
(pure function; move with `Shell/StatusBar/` later). Cursor and diagnostics
stay on `ILabStatus`.

Keep feature files flat (`CompilerState.cs`, `CompilerActions.cs`, … plus
`Components/` / `Services/` when needed). Do not add `State/` / `Actions/` /
`Reducers/` subfolders until a feature has ~30 files. Do not add root
`Services/` / `Managers/` / `Helpers/` / `Interfaces/` / `State/`.

`Lab/` mapping (shrink until gone):

| Current | Target |
|---|---|
| `LabWorkspaceState.cs` | delete eventually (facade) |
| `LabDocuments.cs` | `Features/Documents/` |
| `OutputTabLayout.cs` | `Features/Outputs/` or `Features/Workspace/` |
| `LabSettings.cs` | `Features/Preferences/` |
| `LabTheme*.cs` | `Features/Theme/` |
| `LabUrlSync.cs`, `LabShare.cs` | `Features/Sharing/` |
| `LabLanguageServices.cs`, `LabCursorSync.cs` | `Editor/` |
| `WorkerHost.cs` | `Infrastructure/Worker/` |
| `InputOutputCache.cs`, `TemplateCache.cs` | `Infrastructure/Persistence/` |
| `LabPlatform.cs` | `Infrastructure/Browser/` |
| `LabLogging.cs` | `Infrastructure/Logging/` |
| `LabCatalog.cs` | `Features/Compiler/Services/` |
| `LabFixtures.cs` | `Features/Documents/` |
| `ILabStatus.cs`, `ILabBrand.cs`, `ILabCommands.cs` | delete once selectors/stores replace them |

Drop the `Lab` type prefix as files move (`DocumentsState`, not `LabDocuments`).
Namespaces carry the rest (`DotNetLab.Features.Documents`).

## Later (not now)

- [x] Next Fluxor features only after a real store exists (not wrapping `LabWorkspaceState`) — Compiler, Preferences, Compilation, Documents
- [x] Small scoped feature stores still under `Lab/` until a store is real — CompilerStore, PreferencesStore, CompilationStore, DocumentsStore
- [ ] Move each store + its UI into `Features/` / `Shell/` / `Editor/` / `Infrastructure/`
- [ ] `LabWorkspace` injecting a narrow workspace surface instead of `LabWorkspaceState`
- [ ] `Lab/` empty; `ILab*` gone
- Host-neutral `AddDotNetLabApp()` and a true Server vs WASM split
- `IWorkerTransport` (do not invent a new worker protocol)
- Tests for URL state, documents, tabs, and compile generations
- Extract `Editor/Monaco` to `DotNetLab.Editor.Monaco` only after it has no workspace inject
