# AppNew follow-ups

Track remaining work after the UI folder split and the first ISP cuts
(`ILabStatus`, `ILabBrand`, `ILabCommands`, `ILabPalette`, `ILabSettings`,
`ILabShell`). AppNew stays a WASM rewrite behind `WebAssemblyNew` until
host-neutral `src/App` replacement is an explicit goal.

Keep the current chrome folders until feature stores exist, then move into
[Target folders](#target-folders). `Lab/` is empty. `LabWorkspaceState` lives in
`Features/Workspace/` as the remaining facade (`ILab*` are gone). Do not Fluxor
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
- [x] Fluxor `Features/Compilation` (`Running` / `Stale` / diagnostic counts; worker compile and `CompiledAssembly` stay on the workspace)
- [x] `StatusSelectors` (`CompilationState` + `CompilerState` → right pill / ready / diagnostic counts; `DocumentsState` → template / active file; cursor on `EditorCursor`)
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
- [x] Move `LabLinks` into `Features/Sharing` (`NewIssue` / gist snapshot take compiler + prefs + `LabDocuments`)
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
- [x] `LabBrandBar` drops `ILabBrand` (`LabDocuments.SetTemplate`; paste / settings on `ILabShell`)
- [x] `LabCommandBar` drops `ILabCommands` (compile on `ILabEditor`; SDK pickers on Fluxor; razor toolchain on `ILabSettings`; settings / palette on `ILabShell`)
- [x] `CommandPalette` drops `ILabPalette` (compile / format on `ILabEditor`; paste / settings on `ILabShell`; share / theme / prefs already local)
- [x] `SettingsDialog` drops `ILabSettings` (composer stays; razor / tabs / language services / worker on `ILabWorkspace`; URL persist on `ILabEditor`)
- [x] `StatusBar` drops `ILabStatus` (cursor line/col and diagnostic counts on `ILabWorkspace`; `StatusSelectors` formats them; no cursor Fluxor store)
- [x] `ILabShell` gone (`MainLayout` / brand / command / palette use `ILabWorkspace` for settings / palette / paste)
- [x] `ILabSharing` gone (`LabShare` / `LabUrlSync` / `LabLinks` use `ILabWorkspace`; no Sharing Fluxor store)
- [x] `ILabDocumentHost` gone (`LabDocuments` takes `IDocumentWorkspace`)
- [x] `ILabOutputHost` gone (`OutputTabLayout` takes `IOutputWorkspace`)
- [x] `ILabWorkspace` / `ILabEditor` gone (`LabWorkspace`, `LabCodeEditor`, sharing, documents, outputs, and chrome inject `LabWorkspaceState`)
- [x] Tests for URL state, documents, tabs, and compile generations (`test/AppNewTests`; `LabDocuments` / `OutputTabLayout` take internal host seams; `GenerationCounter` on the workspace)
- [x] Drop catalog and tab-layout pass-throughs on `LabWorkspaceState` (`LabWorkspace` / `SettingsDialog` use `Tabs` and `LabCatalog`; persist still on the workspace)
- [x] Move output-load cache off `LabWorkspaceState` (`OutputLoadCache` + `IOutputLoadHost`; worker `GetOutput` still on the workspace)
- [x] Drop Fluxor getter pass-throughs on `LabWorkspaceState` (`LabWorkspace` / `LabCodeEditor` / `LabLinks` read `IState<T>` / `Documents` / `Tabs`; `Notify` still pulses runtime)
- [x] Document commands persist/LS sync live on `LabDocuments` (`LabWorkspace` calls `Documents.*`; `AfterActiveSourceChanged` still on the host)
- [x] `StatusBar` drops `LabWorkspaceState` (`CompilationState` diagnostic counts; `EditorCursor` for line/col; no cursor Fluxor)

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
- [x] `CompilerEffects` per-kind (or operation-id) generations so a Razor apply cannot leave `RoslynLoading` stuck (`CompilerState.Loading` forever, compile blocked)

## P2

- [x] Introduce `ILabEnvironment` and drop `IWebAssemblyHostEnvironment` from `LabWorkspaceState`
- [x] Make `WorkerHost` `IAsyncDisposable` and protect recreate vs in-flight work (`_epoch` drops stale results; in-process `PostAsync` still does not take `_inProcessGate`)
- [x] Expose `Sources` / `SourceFiles` as `IReadOnly*` and mutate through commands
- [x] Move output-tab persistence out of Razor (`MainLayout`, `LabWorkspace`, `SettingsDialog`)

## P3

- [x] Generate commit / date instead of hardcoded Settings identity (`3f19ab2`)
- [x] Move splitter pointer-move hot path to JS; persist final `Split` only
- [x] Make `LabCodeEditor` `IAsyncDisposable` so teardown cannot race subscriptions

## State direction

Transitional, not the endpoint. Keep Fluxor; do **not** wrap `LabWorkspaceState`.
The leftover problem is two ownership models stacked:

```
mutable session  →  SetXAction(snapshot)  →  Fluxor mirror  →  UI
                         ↑
              LabWorkspaceState.Notify() also re-renders
```

Chrome already reads `IState<T>` / `Documents` / `Tabs` for getter
pass-throughs. The facade still subscribes to every store and fires `Changed`,
so Fluxor is not yet the render bus. `SettingsDialog` / `LabWorkspace` can
double-render (`IState` **and** `Notify`). `StatusBar` is a `FluxorComponent`
plus `EditorCursor.Changed` only.

Target (do not dump keystrokes or `CompiledAssembly` into the store):

```
                 Fluxor
       ┌───────────┼───────────┐
       ▼           ▼           ▼
 CompilerState Preferences  Layout / status flags
       │                       │
       └──────────┐            │
                  ▼            │
             Effects           │
                  │            │
       ┌──────────┴────────────┘
       ▼
Runtime sessions (not Fluxor)

DocumentSession     CompilationSession     EditorSession
source text         Compiled / LastInput   Monaco / cursor
file list owner     compile generations    LS lifecycle
                    output caches
```

`DocumentsState` / `OutputsState` are currently snapshot buses
(`SetDocumentsAction` / `SetOutputsAction` replace the whole record). Pick one
owner per slice: semantic reducers for file list / template / active file /
tab ids, **or** chrome reads the session. Do not keep both. Source text,
Monaco URIs, and `CompiledAssembly` stay on the session.

Status is derived (`Compilation` + `Compiler` + `Documents` → selector). Do
not Fluxor cursor or keystrokes. Diagnostic **counts** (ints, not the
assembly) live on `CompilationState`; cursor is `EditorCursor` (scoped, not
Fluxor). `StatusBar` does not inject `LabWorkspaceState`.

`OnCompilerStoreChanged` mapping compiler key/loading into `Stale` should
become a compilation reducer on compiler/document actions, not a
`StateChanged` subscription.

`Compressor.Uncompress` does **not** throw (garbage slug → `(error)` source
file). `MainLayout` `finally` is enough for a blank page. Invalid-URL UX
(toast + C# default) is later, not a try/catch around Uncompress.

Do **not** replace `LabWorkspaceState` with one `AppState` record. Do **not**
delete the facade in one pass. Do **not** introduce `HybridCache` until
compile is a session. Do **not** extract a `WorkspaceCommands` junk drawer.

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
6. Shrink `LabWorkspaceState` / `Lab/` until both disappear. *(Lab/ empty; catalog/tab pass-throughs gone; output-load cache in `Features/Outputs`; Fluxor getters gone; document commands on `LabDocuments`; facade remains)*
7. Cosmetic leftover: `Header/` → `Shell/Header/` for brand / command / memory
   only. *(done)*
8. Remaining façade cut (do not add more Fluxor first):
   1. Fix `CompilerEffects` generations (P1). *(done; `CompilerApplyGenerations` per SDK / Roslyn / Razor)*
   2. Fold document commands (`Rename` / `Close` / `Add` + persist + LS sync)
      into `LabDocuments` / `DocumentSession`. Decide metadata Fluxor vs
      session-only; stop `SetDocumentsAction` as a full snapshot. *(persist/LS on `LabDocuments`; snapshot bus remains)*
   3. `StatusBar` off the façade: `ErrorCount` / `WarningCount` on
      `CompilationState`; cursor stays off Fluxor. *(done; `EditorCursor`)*
   4. `CompilationSession` for `CompileAsync` / `Compiled` / generations /
      caches. Then `Stale` from compiler actions is a reducer.
   5. `WorkerHost` in-process refcount (keep provider until in-flight
      `HandleAndGetOutputAsync` finishes). Default path is the web worker.
   6. Cosmetic last: `Monaco/` → `Editor/Monaco/`; `IUpdateChecker` next to
      Updates or infrastructure. No `AddDotNetLabApp` / `IWorkerTransport` /
      `DotNetLab.Editor.Monaco` yet.

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
(pure function). Cursor line/col come from `EditorCursor`; diagnostic counts
are on `CompilationState`.

Keep feature files flat (`CompilerState.cs`, `CompilerActions.cs`, … plus
`Components/` / `Services/` when needed). Do not add `State/` / `Actions/` /
`Reducers/` subfolders until a feature has ~30 files. Do not add root
`Services/` / `Managers/` / `Helpers/` / `Interfaces/` / `State/`.

`Lab/` mapping (shrink until gone):

| Current | Target |
|---|---|
| `LabWorkspaceState.cs` | `Features/Workspace/` (facade remains; Fluxor getters gone; do not Fluxor the leftover) |
| `LabDocuments.cs` | `Features/Documents/` |
| `OutputTabLayout.cs` | `Features/Outputs/` |
| `OutputLoadCache.cs` | `Features/Outputs/` (lazy worker load still on the workspace) |
| `LabSettings.cs` | `Features/Preferences/` |
| `LabTheme*.cs` | `Features/Theme/` |
| `LabUrlSync.cs`, `LabShare.cs` | `Features/Sharing/` |
| `LabLanguageServices.cs`, `LabCursorSync.cs`, `EditorCursor.cs` | `Editor/` |
| `WorkerHost.cs` | `Infrastructure/Worker/` |
| `InputOutputCache.cs`, `TemplateCache.cs` | `Infrastructure/Persistence/` |
| `LabPlatform.cs` | `Infrastructure/Browser/` |
| `ILabEnvironment.cs` | `Infrastructure/Browser/` |
| `LabLogging.cs` | `Infrastructure/Logging/` |
| `LabCatalog.cs` | `Features/Compiler/Services/` |
| `LabFixtures.cs` | `Features/Documents/` |
| `ILabStatus.cs` | deleted (`StatusBar` uses Fluxor + `EditorCursor` + `StatusSelectors`; no cursor Fluxor store) |
| `ILabCommands.cs` | deleted (`LabCommandBar` uses `ILabEditor` + Fluxor + `ILabWorkspace` + `ILabShell`) |
| `ILabPalette.cs` | deleted (`CommandPalette` uses `ILabEditor` + `ILabShell`) |
| `ILabSettings.cs` | deleted (`SettingsDialog` / `PreferenceSettings` / `LabCommandBar` use `ILabWorkspace`; URL persist on `ILabEditor`) |
| `ILabBrand.cs` | deleted (`LabBrandBar` uses `LabDocuments` + `ILabShell`) |
| `ILabWorkspace.cs` | deleted (`LabWorkspace` / documents / outputs / sharing / chrome inject `LabWorkspaceState`) |
| `ILabEditor.cs` | deleted (`LabCodeEditor` injects `LabWorkspaceState`; no `DotNetLab.Editor.Monaco` project yet) |
| `ILabSharing.cs` | deleted (`LabShare` / `LabUrlSync` / `LabLinks` use `ILabWorkspace`) |
| `ILabDocumentHost.cs` | deleted (`LabDocuments` takes `ILabWorkspace`) |
| `ILabOutputHost.cs` | deleted (`OutputTabLayout` takes `ILabWorkspace`) |
| `ILabShell.cs` | deleted (`MainLayout` / header / palette use `ILabWorkspace`) |

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
- [x] `ILabBrand` gone (`LabBrandBar` uses `LabDocuments` + `ILabShell`)
- [x] `ILabCommands` gone (`LabCommandBar` uses `ILabEditor` + Fluxor + `ILabWorkspace` + `ILabShell`)
- [x] `ILabPalette` gone (`CommandPalette` uses `ILabEditor` + `ILabShell`)
- [x] `ILabSettings` gone (`SettingsDialog` stays a composer; razor / tabs / LS / worker on `ILabWorkspace`)
- [x] `ILabStatus` gone (`StatusBar` uses Fluxor + `EditorCursor` + `StatusSelectors`; cursor not Fluxor)
- [x] `ILabShell` gone (`MainLayout` / brand / command / palette use `ILabWorkspace`)
- [x] `ILabSharing` gone (`LabShare` / `LabUrlSync` / `LabLinks` use `ILabWorkspace`)
- [x] `ILabDocumentHost` gone (`LabDocuments` takes `IDocumentWorkspace`)
- [x] `ILabOutputHost` gone (`OutputTabLayout` takes `IOutputWorkspace`)
- [x] `ILabWorkspace` / `ILabEditor` gone (`LabWorkspaceState` is the remaining facade; not Fluxor)
- [x] Tests for URL state, documents, tabs, and compile generations (`test/AppNewTests`; generation cancel is `GenerationCounter`)
- [x] Drop catalog and tab-layout pass-throughs on `LabWorkspaceState` (`LabWorkspace` / `SettingsDialog` use `Tabs` and `LabCatalog`)
- [x] Move output-load cache off `LabWorkspaceState` (`OutputLoadCache`; worker `GetOutput` still on the workspace)
- [x] Drop Fluxor getter pass-throughs on `LabWorkspaceState` (`LabWorkspace` / `LabCodeEditor` / `LabLinks` read `IState<T>` / `Documents` / `Tabs`)
- [x] `CompilerEffects` independent generations / operation ids (shared `_generation` can stick `RoslynLoading`)
- [x] Document commands + persist/LS sync off the façade (`LabDocuments`; `SetDocumentsAction` snapshot bus remains)
- [x] `StatusBar` drops `LabWorkspaceState` (`ErrorCount` / `WarningCount` on `CompilationState`; `EditorCursor`; no cursor Fluxor)
- [ ] `CompilationSession` (`CompileAsync`, `Compiled`, generations, caches; not Fluxor)
- [ ] `WorkerHost` in-process request refcount (epoch already drops results)
- [ ] Output layout: semantic Fluxor actions **or** session-only; stop `SetOutputsAction` snapshot
- [ ] Invalid share URL UX (`Uncompress` already does not throw)
- [ ] `Monaco/` → `Editor/Monaco/`; colocate `IUpdateChecker`
- Host-neutral `AddDotNetLabApp()` and a true Server vs WASM split
- `IWorkerTransport` (do not invent a new worker protocol)
- Extract `Editor/Monaco` to `DotNetLab.Editor.Monaco` (`LabCodeEditor` already injects `LabWorkspaceState`)
