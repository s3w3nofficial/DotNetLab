# AppNew follow-ups

Track remaining work after the UI folder split and the first ISP cuts
(`ILabStatus`, `ILabBrand`, `ILabCommands`, `ILabPalette`, `ILabSettings`,
`ILabShell`). AppNew stays a WASM rewrite behind `WebAssemblyNew` until
host-neutral `src/App` replacement is an explicit goal.

Keep the current chrome folders until feature stores exist, then move into
[Target folders](#target-folders). `Lab/` is empty. `LabWorkspaceState` lives in
`Features/Workspace/` and still implements the `ILab*` facades. Do not Fluxor
the current god object; see [State direction](#state-direction).

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
- [x] `LayoutStore` under `Lab/` (`LayoutState.Split`; pointer-move stays in JS; output tabs stay on `OutputTabLayout`)
- [x] Fluxor `Features/Workspace` (`Split`; pointer-move stays in JS; `LabWorkspace` still injects `LabWorkspaceState`)
- [x] `OutputsStore` under `Lab/` (`OutputsState`; `OutputTabLayout` still mutates; worker output stays on the workspace)
- [x] Fluxor `Features/Outputs` (`ActiveOutput` / tab ids / revision; `OutputTabLayout` still mutates; worker output stays on the workspace)
- [x] Move `LabWorkspace` / `EditorGroups` / `EditorDragState` into `Features/Workspace` (`LabCodeEditor` stays in `Workspace/`)
- [x] Move `OutputTabLayout` into `Features/Outputs` (`LabWorkspaceState` still owns the instance; worker output stays on the workspace)
- [x] Move `LabDocuments` / `LabFixtures` into `Features/Documents` (file contents stay on `LabDocuments`; Monaco stays source of truth)
- [x] Move `LabCatalog` into `Features/Compiler` (flat; picker already colocated)
- [x] Move `LabSettings` into `Features/Preferences` (persist stays in effects)
- [x] Move `SettingsDialog` / `SettingRow` into `Features/Preferences` (composer; `ILabSettings` stays on the workspace)
- [x] Move `LabUrlSync` / `LabShare` / `PasteUrlDialog` into `Features/Sharing` (no Fluxor yet; `CommandPalette` stays in `Dialogs/`)
- [x] Move `LabTheme` / `LabThemeService` into `Features/Theme` (no Fluxor yet; theme preference stays on `PreferencesState`)
- [x] Move `Header/` into `Shell/Header/` (`LabBrandBar` / `LabCommandBar` / `MemoryUsageView`; `StatusBar` and `CommandPalette` stay put)
- [x] Move `CommandPalette` into `Shell/CommandPalette/` (`StatusBar` stays put; `ILabPalette` stays on the workspace)
- [x] Move `StatusBar` / `StatusSelectors` into `Shell/StatusBar/` (cursor and diagnostics stay on `ILabStatus`)
- [x] Move `LabLanguageServices` / `LabCursorSync` into `Editor/LanguageServices/` (`LabCodeEditor` and Monaco stay put; apply stays on the workspace)
- [x] Move `WorkerHost` into `Infrastructure/Worker/` (same worker protocol; no `IWorkerTransport`)
- [x] Move `TemplateCache` / `InputOutputCache` into `Infrastructure/Persistence/` (compiled output stays off Fluxor)
- [x] Move `LabPlatform` / `IScreenInfo` into `Infrastructure/Browser/` (`WebAssemblyScreenInfo` stays on the WASM host)
- [x] Move `LabLogging` into `Infrastructure/Logging/` (persist still in Preferences effects; worker snapshots log level on create)
- [x] Move `LabIdentity` into `Features/Preferences` (git commit / date still from assembly metadata)
- [x] Split `LabTypes` into `Features/Compiler` (`SdkOption`), `Features/Outputs` (`OutputTab` / `OutputFileKind`), and `Features/Workspace` (`DropZone` / `TabRename`)
- [x] Move `LabLinks` into `Features/Sharing` (`NewIssue` / gist snapshot still take `LabWorkspaceState`)
- [x] Delete unused `StateStore<T>` and `ElementRect` (Fluxor replaced the Lab stores; `ElementRect` had no callers)
- [x] Move `ILabEnvironment` / `LabEnvironment` into `Infrastructure/Browser/` (`AppBuilder` still wires the WASM host; no `AddDotNetLabApp`)
- [x] Move `ILab*` interfaces next to the chrome that injects them (`LabWorkspaceState` still implements them)
- [x] `LabWorkspace` injects `ILabWorkspace` (`LabCodeEditor` still injects `LabWorkspaceState`; catalog/fixture statics come from `LabCatalog` / `LabFixtures`)
- [x] `LabCodeEditor` injects `ILabEditor` (file stays in `Workspace/`; Monaco stays source of truth; apply stays on the workspace)
- [x] Move `LabCodeEditor` into `Editor/` (`ILabEditor` already there; no `DotNetLab.Editor.Monaco` project)
- [x] Sharing (`LabShare` / `LabUrlSync` / `LabLinks`) uses `ILabSharing` (no Fluxor; `SavedState` still from Shared)
- [x] `LabDocuments` uses `ILabDocumentHost` (`OutputTabLayout` still takes `LabWorkspaceState`)
- [x] `OutputTabLayout` uses `ILabOutputHost` (`LabWorkspaceState` still constructs it)
- [x] Move `LabWorkspaceState` into `Features/Workspace/` (`Lab/` empty; `ILab*` still implemented)

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
DocumentsStore     CompilerStore     WorkspaceStore     PreferencesStore
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
   slice is ready to become a Fluxor feature. *(done; `StateStore<T>` deleted
   after those slices became Fluxor features)*
5. When a store is real, colocate its UI with it (e.g. `CompilerPicker` +
   `CompilerSection` move with `CompilerStore`, not before) and optionally
   convert that slice to Fluxor the same way Updates was converted.
6. Shrink `LabWorkspaceState` / `Lab/` until both disappear. *(Lab/ empty; facade remains)*
7. Cosmetic leftover: `Header/` → `Shell/Header/` for brand / command / memory
   only. *(done)*

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
│   ├── Browser/              LabPlatform, IScreenInfo, ILabEnvironment
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
`CompilationState` + `CompilerState` + `DocumentsState` → `Shell/StatusBar/StatusSelectors.cs`
(pure function). Cursor and diagnostics stay on `ILabStatus`.

Keep feature files flat (`CompilerState.cs`, `CompilerActions.cs`, … plus
`Components/` / `Services/` when needed). Do not add `State/` / `Actions/` /
`Reducers/` subfolders until a feature has ~30 files. Do not add root
`Services/` / `Managers/` / `Helpers/` / `Interfaces/` / `State/`.

`Lab/` mapping (shrink until gone):

| Current | Target |
|---|---|
| `LabWorkspaceState.cs` | `Features/Workspace/` (facade; delete once `ILab*` go) |
| `LabDocuments.cs` | `Features/Documents/` |
| `OutputTabLayout.cs` | `Features/Outputs/` or `Features/Workspace/` |
| `LabSettings.cs` | `Features/Preferences/` |
| `LabTheme*.cs` | `Features/Theme/` |
| `LabUrlSync.cs`, `LabShare.cs` | `Features/Sharing/` |
| `LabLanguageServices.cs`, `LabCursorSync.cs` | `Editor/` |
| `WorkerHost.cs` | `Infrastructure/Worker/` |
| `InputOutputCache.cs`, `TemplateCache.cs` | `Infrastructure/Persistence/` |
| `LabPlatform.cs` | `Infrastructure/Browser/` |
| `ILabEnvironment.cs` | `Infrastructure/Browser/` |
| `LabLogging.cs` | `Infrastructure/Logging/` |
| `LabCatalog.cs` | `Features/Compiler/Services/` |
| `LabFixtures.cs` | `Features/Documents/` |
| `ILabStatus.cs`, `ILabBrand.cs`, `ILabCommands.cs` | delete once selectors/stores replace them (files now sit with Shell chrome; workspace still implements) |
| `ILabWorkspace.cs` | `Features/Workspace/` (pane surface; `LabWorkspaceState` still implements) |
| `ILabEditor.cs` | `Editor/` (`LabCodeEditor` injects it; razor lives in `Editor/` too; no new project yet) |
| `ILabSharing.cs` | `Features/Sharing/` (`LabShare` / `LabUrlSync` / `LabLinks`; workspace still implements) |
| `ILabDocumentHost.cs` | `Features/Documents/` (`LabDocuments` constructed with the host; workspace still implements) |
| `ILabOutputHost.cs` | `Features/Outputs/` (`OutputTabLayout` constructed with the host; workspace still implements) |

Drop the `Lab` type prefix as files move (`DocumentsState`, not `LabDocuments`).
Namespaces carry the rest (`DotNetLab.Features.Documents`).

## Later (not now)

- [x] Next Fluxor features only after a real store exists (not wrapping `LabWorkspaceState`) — Compiler, Preferences, Compilation, Documents, Workspace, Outputs
- [x] Small scoped feature stores still under `Lab/` until a store is real — CompilerStore, PreferencesStore, CompilationStore, DocumentsStore, LayoutStore, OutputsStore
- [ ] Move each store + its UI into `Features/` / `Shell/` / `Editor/` / `Infrastructure/`
- [x] `LabWorkspace` injecting a narrow workspace surface instead of `LabWorkspaceState`
- [x] `LabCodeEditor` injecting a narrow editor surface instead of `LabWorkspaceState`
- [x] Move `LabCodeEditor` into `Editor/` (no `DotNetLab.Editor.Monaco` project yet)
- [x] Sharing injecting a narrow surface instead of `LabWorkspaceState`
- [x] `LabDocuments` using a narrow host instead of `LabWorkspaceState`
- [x] `OutputTabLayout` using a narrow host instead of `LabWorkspaceState`
- [x] `Lab/` empty (`LabWorkspaceState` in `Features/Workspace/`)
- [ ] `ILab*` gone
- Host-neutral `AddDotNetLabApp()` and a true Server vs WASM split
- `IWorkerTransport` (do not invent a new worker protocol)
- Tests for URL state, documents, tabs, and compile generations
- Extract `Editor/Monaco` to `DotNetLab.Editor.Monaco` (`LabCodeEditor` already injects `ILabEditor`)
