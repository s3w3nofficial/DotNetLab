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
 Compilation   Updates      Options / ActiveOutput
      │
      ▼
 runtime sessions
 ┌────┼──────────────┐
 ▼    ▼              ▼
Docs Compilation   Outputs
     Session        Session / Tabs
        │
        ▼
    WorkerHost
```

| Fluxor (facts) | Runtime (resources) |
|---|---|
| `CompilerState` | `LabDocuments` |
| `CompilationState` (`Running` / `Stale` / counts) | `CompilationSession` (`Compiled`, generations) |
| `CompilationOptionsState` | `OutputSession` |
| `OutputState` (`ActiveOutput`) | `OutputTabLayout` |
| `PreferencesState` | `LabLanguageServices` |
| `WorkspaceState` (`Split`) | `WorkerHost` / `EditorCursor` |
| `DocumentMetadataState` | `LabDocuments` (file text / URIs) |
| `UpdateState` | |

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
| Channels | Latest-wins compile / persist coalescing | Incremental LS deltas, query overlap, Cancel overlap |
| Sessions | Documents, compiled output, Monaco, LS | Share-URL fields |
| `ICompilationCache` | IndexedDB L1 + remote HTTP L2 reuse | TemplateCache, OutputSession, HybridCache |

`Cancel` still goes straight to `WorkerHost` (serializing it against the
request it aborts is wrong). Incremental document mutations must be **posted
immediately**, like master — not parked on an app-side Channel that queries
bypass. Do not put an ordered queue in the worker either. Language services
stay as close to master as possible.

## Do not

- Fluxor `LabWorkspaceState`, keystrokes, Monaco, worker handles, or
  `CompiledAssembly`
- Channel every worker message (that would serialize Cancel against the
  request it aborts)
- Park Monaco deltas on an app-side unbounded Channel that waits for each
  worker ack while completion / hover / tokens / code actions skip it
  (that was item 2 as shipped — a regression vs master; undone in 7)
- `DropOldest` on language-service deltas (they are incremental)
- `skipDebounce` for trigger-character completions until posting order /
  versions are solid (`.` / `(` / `=` bypass the 1s throttle)
- Mistake `ConfigureAwait(false)` on a Channel reader for “this is off the
  UI thread” (Blazor WASM is still one browser thread)
- Reintroduce Documents/Outputs Fluxor snapshot mirrors
- Replace `TemplateCache` or `OutputSession` with HybridCache or IndexedDB
- Pretend `vsinsertions.azurewebsites.net` is `IDistributedCache`
- Pretend IndexedDB is HybridCache L1 or `IDistributedCache`
- Inject `HybridCache` or IndexedDB into `CompilationSession`
- Invent interactive Blazor Server
- Extract `DotNetLab.Editor.Monaco`
- Add a worker `LanguageSession` or otherwise serialize LS work in the worker
- Change language services further — keep them as close to master as possible
- Split `CompilationOptionsState` or change `Compiler` / `GetOutput` for format
  prefs — that is deferred (needs Shared + Compiler, not App-only)
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

### 2. Language mutation queue — shipped; undone in 7

`LanguageMutationQueue` serializes incremental deltas on an unbounded Channel
and **awaits each worker ack** before posting the next. Queries do not use it.

```
Monaco "." 
  ├─ edit ──→ Channel ── wait previous ack ──→ WorkerHost
  └─ completion (skipDebounce) ──────────────→ WorkerHost now
```

Roslyn can complete against the **previous** document. That matches
one-character-behind IntelliSense, stale hover / tokens / diagnostics, and
backlog during fast typing (unbounded + producer faster than 30–100 ms
round-trips).

Master does **not** add this queue. `LanguageServicesClient` posts
`OnDidChangeModelContent` immediately via `WorkerController`, then
fire-and-forgets diagnostics. The UI does not await the ack. The worker
`postMessage` / JS bridge is essentially the same; do not start by rewriting
`WorkerHost` serialization.

The comment on `OnDidChangeModelContentAsync` (“keystrokes must not sit
behind completion/hover”) inverted the problem. Queries skipping the Channel
is the causality bug. A query barrier (`EnqueueBarrierAsync` before
completion) would confirm it — correct but IntelliSense then waits on the
mutation backlog. Do not keep that as the design.

`DropOldest` is still wrong for deltas. Recreate still must not replay
leftover deltas against a new worker. Those constraints survive; the
UI-side round-trip queue does not.

Also in the WorkerHost pass (keep):

- [x] Do not reset `_messageId` on recreate (late old-worker `#1` can complete
      new `#1`)
- [x] Ignore callbacks whose worker epoch ≠ `Volatile.Read(ref _epoch)`
- [x] `cancellationToken.ThrowIfCancellationRequested()` at the start of
      `SendAsync` (already-cancelled tokens Register immediately and send
      `Cancel` before the request)

### 3. Compile-in-flight guard — done

`CompilationSession` no longer uses `_compileInFlight`. Fluxor `Running` is only
set when the UI should look busy. A quiet cached follow-up compile cannot overlap
another compile: item 4's scheduler is the mutex. Item 8 waits out
`Compiler.Loading` instead of dropping the consumed pulse.

`ApplySavedStateCoreAsync` still fire-and-forgets `AfterDocumentsChangedAsync`
then template cache / compile; the scheduler is the mutex those races
needed.

### 4. Compilation scheduler Channel — done

`CompilationScheduler` is bounded 1 / `DropOldest`. `CompileAsync` enqueues;
the session still executes. A mailbox coalesces flags so DropOldest does not
lose a user `storeInCache`. In-flight work is cancelled when a newer request
arrives; generation skips committing a stale result. User compiles dispatch
`CompileRequestedAction` (item 14). Quiet `ApplySavedState` still calls
`CompileAsync` so it does not look like a user request.

The single reader serializes `CompileCoreAsync`. A compile that arrives while
`Compiler.Loading` waits until idle (or until a newer generation cancels it).
`ApplySavedState` also waits for idle then compiles; a user Compile during an
SDK apply is not dropped.

### 5. `ICompilationCache` — done

HybridCache L1 is in-process RAM. That duplicates `CompilationSession.Compiled`
and dies on reload. IndexedDB is the browser L1; the Azure HTTP cache is L2.
Stampede protection is a per-key in-flight `Get` on `ICompilationCache`, not
the HybridCache package.

```
Infrastructure/Caching/Compilation/
    ICompilationCache.cs        ← namespace stays Features.Compilation
    CompilationCache            ← stampede + L1 then L2
    IndexedDbCompilationCache   ← L1 (thin `netLabCompileCache` JS)
    RemoteCompilationCache      ← L2 (rename of InputOutputCache)
Infrastructure/Caching/Template/
    TemplateCache               ← gzipped snapshots; namespace stays Persistence
```

```
Compile request
 ├─ known template? → TemplateCache (keep; three gzipped payloads)
 └─ ICompilationCache
      ├─ IndexedDB L1
      └─ miss → RemoteCompilationCache (HTTP)
           └─ miss → WorkerHost compile → StoreAsync to L1 + L2
```

Do **not** negative-cache every `null` — `GetAsync` returns null for miss
**and** HTTP/parse/IDB errors. Do not write those to IndexedDB. `EnableCaching`
still gates Get/Store at the session. Evict IndexedDB by LRU (max entries);
quota failure is a miss. The IDB **database** name is versioned
(`netlab-compile-v1` in `lab-persist.js`). The **key** is
`v{Schema}-` + `XxHash128(ToCacheSlug())` (item 9). Native/store later
no-ops L1 behind the same interface.

There is no in-process map of *previous* slugs, so sequential hits after
stampede ends go to IndexedDB. That is acceptable. Do **not** add the
HybridCache package for an in-process map.

Do not wrap IndexedDB or the Azure HTTP cache as `IDistributedCache`.

`OutputSession` is session state (current compiled assembly + tab +
generation + Monaco URIs), not IndexedDB/HTTP.

### 6. Shrink the facade — done

Deleted dead `StatusChanged` / `NotifyStatus`. Remaining `Workspace.Changed`
subscribers now listen to what they actually read:

- `LabBrandBar` → `LabDocuments.Changed` (template)
- `SettingsDialog` → `OutputTabLayout.Changed` (tab settings rows)
- `LabCodeEditor` → `PreferencesState` (Monaco theme / vim / keyboard)
- `LabWorkspace` → `Changed` for session chrome, plus Fluxor for prefs /
  split / compilation / options

URL / settings / output-tab writes go through `PersistenceQueue` (bounded 1 /
`DropOldest` + 50ms debounce). Snapshots are captured at execute time.
`_suppressUrlPersist` still skips URL writes during `ApplySavedState`.

After options + ActiveOutput left the facade, `LabWorkspaceState` still
applies/captures `SavedState` and wires persist / language / worker. Dialogs,
pane split, and rendered-HTML are not session. Do not delete the facade as a
line-count goal.

### 7. Language-service baseline — done

Posting order matches master again. `LanguageMutationQueue` is gone. Completions
always go through debounce. `DebounceAsync` cancels in-flight handlers via
`debounceToken`. Diagnostics skip `SetModelMarkers` when Monaco's
`getAlternativeVersionId` moved while `GetDiagnostics` was in flight (no worker
protocol change). Confirm `Starting compilation web worker.` when testing
IntelliSense.

- [x] Remove `LanguageMutationQueue` from `OnDidChangeModelContent`. Call
      `SendAsync` immediately (do not await it from the keystroke handler).
      Fire-and-forget diagnostics **after** that mutation task completes, like
      master. Workspace snapshots follow the same “post now” rule.
- [x] Remove `skipDebounce` for `CompletionTriggerKind.TriggerCharacter`.
      Master always runs completion through debounce. The trigger list
      includes `.` `(` `=` space; bypass + Channel is the worst ordering.
      Do not special-case `.` until versions/order are solid.
- [x] `DebounceAsync`: after the wait, call `handler(args, debounceToken)` so a
      later edit cancels in-flight diagnostics / completion, not only the
      delay. The debounce CTS is already linked to the user token. Same bug
      exists in master; it matters more with overlapping LS traffic.
- [x] Document-version checks on diagnostics (and later hover / tokens /
      signature help / code actions). Completions already discard stale
      results via `model.getAlternativeVersionId()` in JS. Master has no
      diagnostics version guard either — add it here.

Do not reopen HybridCache, `DropOldest` on LS deltas, a worker LS session,
or deleting the facade. Do not change language services further.

### 8. Remaining Channel races — done

Compile / persist leftovers. Independent of the LS undo.

- [x] `PersistenceQueue`: take coalesced `PersistKind` flags **and** waiters in
      one lock after the debounce. Taking flags then waiters on separate locks
      could complete a waiter whose kind had not run yet.
- [x] `CompileCoreAsync`: wait while `Compiler.Loading` (or until a newer
      generation cancels) instead of returning. The scheduler has already
      consumed the pulse; dropping it completed waiters and lost the click.
      `_compileInFlight` is gone — the scheduler is the only gate.

### 9. Compilation cache key schema — done

`CompilationCacheKey.Schema` (currently 1) prefixes the hashed slug as
`v1-{hex}` — one path segment for `/api/cache/add/{key}`. Unprefixed HTTP /
IndexedDB rows miss and are replaced on the next store. Bump `Schema` when
`CompiledAssembly` / Worker JSON cannot safely reuse older entries; keep it
when JSON stays backward compatible (`InputOutputCacheTests.BackwardsCompatibility`).
The IDB database name still wipes the browser on bump; the key prefix versions
the shared HTTP cache. SHA-256 instead of `XxHash128` stays optional later.

### 10. Monaco text patch — done

Each keystroke still needs a new C# `string` for `LabDocuments`. Concat of three
spans is one allocation of the document; that is cheaper than Monaco `GetValue`
JS interop. Measured on this machine (Release-equivalent Debug test host):

| Work | Concat loop | One pass |
|---|---|---|
| C# template, 1 insert (20k×) | ~1.5µs | same path (~0.3µs, no LINQ sort) |
| 64KiB, 1 insert | ~20–45µs | same (Concat) |
| 512KiB, 1 insert | ~0.4ms | same (Concat) |
| 64KiB, 80 disjoint edits (400×) | ~2.6ms | ~33µs |

A rope / gap buffer is not worth it. The real cost was **N full copies in one
Monaco event** (format, multi-cursor, paste). `MonacoTextPatch` walks original
offsets once; the single-edit path keeps Concat.

### 11. Semantic highlighting once — done

`InitializeLanguageServicesAsync` enables semantic highlighting once. The JS
hook (`onDidCreateEditor`) covers editors created later. `addAction` is skipped
when `debug-semantic-token` is already present. `LabCodeEditor` init no longer
walks every Monaco instance.

### 12. Native in-process worker — done

App's default `UnsupportedWorkerTransport` does not support a background
worker. `WorkerHost` then starts `WorkerServices` in-process instead of
`CreateWorker` (which still throws if called). Browser
`BrowserWorkerTransport` still supports the web worker. The Background worker
setting is hidden when the transport cannot create one.

### 13. Document metadata Fluxor — done

Template, active file, and open names are `DocumentMetadataState`. File text
stays on `LabDocuments`. Consumers: template menu, source tabs, status bar.
`SetDocumentsAction` snapshot buses stay gone.

### 14. CompileRequestedAction — done

Toolbar, palette, and Ctrl/Cmd+S dispatch `CompileRequestedAction`. The effect
calls `CompilationSession.CompileAsync`; the scheduler stays the mutex.
Quiet `ApplySavedState` compiles still call the session directly.

### 15. OutputSession — done

`OutputLoadCache` was session state (loaded tabs, Monaco URIs, error-list
switch), not IndexedDB/HTTP. It is `OutputSession`; the workspace property is
`Outputs`.

### 16. Channel reader DisposeAsync — done

`CompilationScheduler` and `PersistenceQueue` keep the Channel reader Task.
`DisposeAsync` completes the writer and waits for the loop. There is no sync
`Dispose` — a wait on the WASM UI thread would deadlock. In-flight persists
are not cancelled; dispose waits until that execute returns. Compile dispose
still cancels the in-flight CTS, then waits until execute returns.
`LabWorkspaceState` / `CompilationSession` are `IAsyncDisposable` so DI waits.

### 17. More facade shrink — done

Settings / palette / paste URL live on `LabDialogs`. Pane split dispatches
`SetSplitAction` from `LabWorkspace`. Rendered HTML is a field on that
component. Dead `MarkStale` / public `Running` / `Stale` wrappers are gone;
those facts stay on `CompilationState` (host interfaces still read them).

## Later (not now)

- [ ] Drop `LabWorkspaceState` entirely once it is only glue — decide then,
      do not pre-delete

## Deferred

### Compile identity vs output format

Toggling Full IL, Operations, Bound nodes, Symbols, custom-attribute blobs,
sequence points, or the Razor declaration document today starts a **full
Roslyn compile**. Those flags live on `CompilationInput.Preferences`.
`CanReuseLastCompile` is `CompilationInput` record equality, so any checkbox
change is a new input. Lazy `GetOutput` lambdas in `Compiler` close over
`compilationInput.Preferences` at compile time (`ShowOperations`, `FullIl`,
…). `SavedState.ToCacheSlug` hashes the same prefs, so IndexedDB / HTTP miss
too.

Two groups are mixed on one record:

| Compile-affecting | Format-only (should not re-run Roslyn) |
|---|---|
| sources, `Configuration` | `ShowOperations`, `ShowBoundNodes` |
| `RazorToolchain`, `RazorStrategy` | `ShowSymbolKinds`, `FullIl` |
| diagnostic flags (`ExcludeSingleFileNameInDiagnostics`, `IncludeHiddenDiagnostics`) | `DecodeCustomAttributeBlobs`, `ShowSequencePoints`, `ShowDeclarationDocument` |

The UI already stores both groups on `CompilationOptionsState` / `SavedState`.
Splitting Fluxor first does not save work: the next Compile still sends a
different `CompilationInput` and the worker still formats with the prefs
captured at compile.

The real change is in **Shared + Compiler**, not App:

1. Lazy formatters / `GetOutput` take *current* format prefs (pass them into
   `LoadAsync`, do not close over `CompilationInput.Preferences`).
2. Compilation identity (`CanReuseLastCompile`, cache slug) excludes format
   prefs. Diagnostic flags stay on the identity if they change the diagnostic
   list.
3. App then reuses `CompilationSession.Compiled`, clears `OutputSession`, and
   reloads the displayed tab.

Do not start this from App. An attempt that patched `Compiler` / `ICompiler`
for current prefs was reverted — stay UI-only until someone owns that
compiler work.
