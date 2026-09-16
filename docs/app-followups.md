# App follow-ups

Remaining work after the AppNew cutover (`src/App`, hosted by `src/WebAssembly`
and `src/Server`). The old parallel stack is gone. Native/store apps are still
later (`docs/native-apps.md`).

`LabWorkspaceState` is still the saved-state / orchestration facade. Do **not**
Fluxor it. Do **not** dump keystrokes, Monaco handles, worker handles, or
`CompiledAssembly` into the store.

## Where we are

Fluxor holds UI facts. Runtime sessions hold live resources. WorkerHost is the
process boundary.

```
                Fluxor
      ┌──────────┼─────────────┐
      ▼          ▼             ▼
 Compiler    Preferences    Workspace (Split)
 Compilation   Updates
      │
      ▼
 runtime sessions
 ┌────┼──────────────┐
 ▼    ▼              ▼
Docs Compilation   Outputs
     Session        Cache / Tabs
        │
        ▼
    WorkerHost
```

| Fluxor (facts) | Runtime (resources) |
|---|---|
| `CompilerState` | `LabDocuments` |
| `CompilationState` (`Running` / `Stale` / counts) | `CompilationSession` (`Compiled`, generations) |
| `PreferencesState` | `OutputLoadCache` |
| `WorkspaceState` (`Split`) | `OutputTabLayout` |
| `UpdateState` | `LabLanguageServices` |
| | `WorkerHost` / `EditorCursor` |

Documents and output-tab **mirrors** were removed on purpose. Do not bring
`SetDocumentsAction` / `SetOutputsAction` snapshot buses back.

`CompilationState` is “what the UI believes.” `CompilationSession` is “what
compiler work currently exists.” Keep that split.

## Target ownership

Use each tool for one job. Do not turn Fluxor, Channels, or HybridCache into
another catch-all.

```
UI
 │
 ▼
Fluxor          "what is true right now?"
 │
Effects
 ├──────────────┬─────────────────┐
 ▼              ▼                 ▼
Channels     Sessions          caches
"work order" "runtime resources" "reuse"
 │              │
 └──────┬───────┘
        ▼
    WorkerHost
```

| Mechanism | Owns | Does not own |
|---|---|---|
| Fluxor | Serializable UI facts, stale/running flags | Handles, deltas, `CompiledAssembly` |
| Channels | Mutation order / latest-wins scheduling | Query overlap, Cancel overlap |
| Sessions | Documents, compiled output, Monaco, LS | Share-URL fields |
| `ICompilationCache` | Remote compiled-output reuse | TemplateCache, OutputLoadCache |

Queries (completion, hover, definition, semantic tokens) and `Cancel` go
straight to `WorkerHost`. Mutations do not.

## Do not

- Fluxor `LabWorkspaceState`, keystrokes, Monaco, worker handles, or
  `CompiledAssembly`
- Channel every worker message (that would serialize Cancel against the
  request it aborts)
- `DropOldest` on language-service deltas (they are incremental)
- Reintroduce Documents/Outputs Fluxor snapshot mirrors
- Replace `TemplateCache` or `OutputLoadCache` with HybridCache
- Pretend `vsinsertions.azurewebsites.net` is `IDistributedCache`
- Inject `HybridCache` into `CompilationSession`
- Invent interactive Blazor Server
- Extract `DotNetLab.Editor.Monaco`
- Commit / push unless asked

Keep `WasmEnableWebcil=false` and Fluent UI 5. Native `<button>` stays for
tabs / palette / segmented radios. `MainLayout` stays in `Layout/`.
`Compressor.Uncompress` does not throw.

## Sequence

### 1. Compilation options + active output into Fluxor — done

`CompilationOptionsState` owns the share-URL compilation flags as **enums**.
`OutputState` owns `ActiveOutput`. `LabWorkspaceState` still applies/captures
`SavedState` and persists URL when those stores change. `ShowRenderedHtml`
stays local. `OutputTabLayout` still owns tab order.

### 2. Language mutation queue — done

`LabLanguageServices` used to fire-and-forget:

```
SendAsync(OnDidChangeModelContent / OnDidChangeWorkspace)
UpdateDiagnosticsAsync(...)
```

`WorkerHost` correctly allows concurrent in-process requests so Cancel can
overlap compile. That is wrong for incremental Monaco deltas.

```
MUTATIONS                         QUERIES
model change ─┐                   completion ──┐
model change ─┼─ Channel          hover ───────┤
workspace ────┘  single reader    definition ──┤
        │                         semantic ────┘
        ▼                                │
  worker applies                         │
        │                                │
        ▼                                │
  diagnostics debounce ──────────────────┤
                                         ▼
                                    WorkerHost
```

Use `Channel.CreateUnbounded` (or bounded `Wait`). **Not** `DropOldest`.
If the queued payload later becomes full document text + version, latest-wins
becomes possible. Today it is deltas, so dropping one corrupts Roslyn.

Keep the queue in `LabLanguageServices`, not `WorkerHost`. `WriteAsync` from
Monaco must return without waiting (keystrokes must not sit behind
completion/hover).

After `await mutation.ApplyAsync`, then schedule diagnostics. Do not race
`GetDiagnostics` against the corresponding document change. The one-second
debounce currently hides this; `skipDebounce` after compilation makes it
worse.

On `WorkerHost.RecreateAsync`, drain/cancel the queue and send a full
`OnDidChangeWorkspace`. Leftover deltas against a new worker are poison.

Also in this WorkerHost pass:

- [x] Do not reset `_messageId` on recreate (late old-worker `#1` can complete
      new `#1`)
- [x] Ignore callbacks whose worker epoch ≠ `Volatile.Read(ref _epoch)`
- [x] `cancellationToken.ThrowIfCancellationRequested()` at the start of
      `SendAsync` (already-cancelled tokens Register immediately and send
      `Cancel` before the request)

### 3. Compile-in-flight guard — done

`CompilationSession` gates work with `_compileInFlight`. Fluxor `Running` is only
set when the UI should look busy. A quiet cached follow-up compile can no
longer overlap another compile and both write `LastInput` /
`_liveCompiledInput` / `Compiled`.

`ApplySavedStateCoreAsync` still fire-and-forgets `AfterDocumentsChangedAsync`
then template cache / compile; the in-flight gate is the mutex those races
needed. A latest-wins scheduler is the next item.

### 4. Optional compilation scheduler Channel

`CompilationInput` is a full snapshot, so latest-wins is valid. Only after
the in-flight guard:

```
Channel      → what executes next (bounded 1, DropOldest)
Cancel       → stop work that is no longer interesting
Generation   → do not commit a stale result
```

`DropOldest` does not cancel the running compile; cancel `A` when `C`
arrives. Fluxor still only records requested / running / completed / failed /
stale. Do not compile from a reducer.

Do not route every `CompileAsync` through `CompileRequestedAction` until the
session is already gated.

### 5. `ICompilationCache` (HybridCache later, maybe never in WASM)

```
Features/Compilation/ICompilationCache.cs
Infrastructure/Caching/   (or Persistence/)
    RemoteCompilationCache   ← rename of InputOutputCache
    HybridCompilationCache   ← only if measured
```

Lookup hierarchy:

```
Compile request
 ├─ known template? → TemplateCache (keep; three gzipped payloads)
 └─ ICompilationCache
      ├─ optional L1
      └─ RemoteCompilationCache (HTTP)
           └─ miss → WorkerHost compile → store remote
```

Do **not** put HybridCache in the WASM app unless you measure repeat HTTP or
stampede. `CompiledAssembly` is large; L1 would duplicate
`CompilationSession.Compiled`. Extra WASM payload for a server L1/L2 story.

If L1 is added: `GetOrCreateAsync` stampede protection is the useful part.
Do **not** negative-cache every `null` — current `LoadAsync` returns null for
miss **and** HTTP/parse errors. Distinguish those before a short Miss TTL.

`IDistributedCache` belongs on a **server** host (Redis) later, not as a
wrapper around the Azure HTTP cache API.

Leave `OutputLoadCache` as session state (current compiled assembly + tab +
generation + Monaco URIs). Rename to `OutputSession` only if useful.

### 6. Shrink the facade

- [ ] Delete dead `LabWorkspaceState.StatusChanged` / `NotifyStatus` /
      `IDocumentWorkspace.NotifyStatus` (`LabDocuments.SetSource` still calls
      it; nothing subscribes; StatusBar reads Fluxor + `EditorCursor` +
      `LabDocuments`)
- [ ] Replace remaining `Workspace.Changed` subscribers with the actual
      dependency (`LabWorkspace`, `LabCodeEditor`, `SettingsDialog`,
      `LabBrandBar`). Brand bar only needs document/template changes.
- [ ] Persistence Channel last (URL / settings / output tabs are snapshots,
      so latest-wins + debounce is valid). `_suppressUrlPersist` stays
      orchestration.

After options + ActiveOutput leave the facade, `LabWorkspaceState` should
mostly apply/capture `SavedState` and wire persist / settings / palette.
Whether that leftover coordinator should exist is a later question. Do not
delete it in the same pass as the language queue.

## Later (not now)

- [ ] Native host (`IWorkerTransport` in-process; default
      `UnsupportedWorkerTransport.CreateWorker` throws)
- [ ] Document metadata Fluxor (`ActiveDocument` / template / open names)
      only with a real consumer
- [ ] `WorkerState` for `WorkerError` only if more than one UI surface needs it
- [ ] Persistence Channel for URL / settings / tab writes
- [ ] HybridCache L1 in front of the remote compilation cache (WASM: only if
      measured; server Redis `IDistributedCache` is a host concern)
- [ ] Compile Fluxor `CompileRequestedAction` → scheduler after the in-flight
      guard exists
- [ ] Rename `OutputLoadCache` → `OutputSession` if the name still misleads
- [ ] Drop `LabWorkspaceState` entirely once it is only glue — decide then,
      do not pre-delete
