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
     Session        Cache / Tabs
        │
        ▼
    WorkerHost
```

| Fluxor (facts) | Runtime (resources) |
|---|---|
| `CompilerState` | `LabDocuments` |
| `CompilationState` (`Running` / `Stale` / counts) | `CompilationSession` (`Compiled`, generations) |
| `CompilationOptionsState` | `OutputLoadCache` |
| `OutputState` (`ActiveOutput`) | `OutputTabLayout` |
| `PreferencesState` | `LabLanguageServices` |
| `WorkspaceState` (`Split`) | `WorkerHost` / `EditorCursor` |
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
| `ICompilationCache` | IndexedDB L1 + remote HTTP L2 reuse | TemplateCache, OutputLoadCache, HybridCache |

`Cancel` still goes straight to `WorkerHost` (serializing it against the
request it aborts is wrong). Incremental document mutations must be **posted
immediately**, like master — not parked on an app-side Channel that queries
bypass. If LS ordering comes back, it belongs in the worker (versioned
messages or a worker-side session queue), not in front of `postMessage`.

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
- Replace `TemplateCache` or `OutputLoadCache` with HybridCache or IndexedDB
- Pretend `vsinsertions.azurewebsites.net` is `IDistributedCache`
- Pretend IndexedDB is HybridCache L1 or `IDistributedCache`
- Inject `HybridCache` or IndexedDB into `CompilationSession`
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

`CompilationSession` gates work with `_compileInFlight`. Fluxor `Running` is only
set when the UI should look busy. A quiet cached follow-up compile can no
longer overlap another compile and both write `LastInput` /
`_liveCompiledInput` / `Compiled`. Item 4's scheduler is now the real
single-flight; item 8 can drop the Interlocked gate.

`ApplySavedStateCoreAsync` still fire-and-forgets `AfterDocumentsChangedAsync`
then template cache / compile; the scheduler is the mutex those races
needed.

### 4. Compilation scheduler Channel — done

`CompilationScheduler` is bounded 1 / `DropOldest`. `CompileAsync` enqueues;
the session still executes. A mailbox coalesces flags so DropOldest does not
lose a user `storeInCache`. In-flight work is cancelled when a newer request
arrives; generation skips committing a stale result. Callers are not routed
through `CompileRequestedAction`.

The single reader already serializes `CompileCoreAsync`. `_compileInFlight` is
belt-and-suspenders, not a second mutex. Do not drop a consumed request just
because `Compiler.Loading` is true (item 8) — waiters complete and the click
is gone. `ApplySavedState` waits for idle then compiles; a user Compile during
an SDK apply does not.

### 5. `ICompilationCache` — done

HybridCache L1 is in-process RAM. That duplicates `CompilationSession.Compiled`
and dies on reload. IndexedDB is the browser L1; the Azure HTTP cache is L2.
Stampede protection is a per-key in-flight `Get` on `ICompilationCache`, not
the HybridCache package.

```
Features/Compilation/
    ICompilationCache.cs
Infrastructure/Caching/
    CompilationCache            ← stampede + L1 then L2
    IndexedDbCompilationCache   ← L1 (thin `netLabCompileCache` JS)
    RemoteCompilationCache      ← L2 (rename of InputOutputCache)
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
(`netlab-compile-v1` in `lab-persist.js`). The **key** is still
`XxHash128(ToCacheSlug())` with no schema prefix — item 9. Native/store later
no-ops L1 behind the same interface.

There is no in-process map of *previous* slugs, so sequential hits after
stampede ends go to IndexedDB. That is acceptable. A tiny last-N map on
`ICompilationCache` would be optional later. Do **not** add the HybridCache
package for it.

`IDistributedCache` belongs on a **server** host (Redis) later, not as a
wrapper around IndexedDB or the Azure HTTP cache API.

Leave `OutputLoadCache` as session state (current compiled assembly + tab +
generation + Monaco URIs). Rename to `OutputSession` only if useful.

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

After options + ActiveOutput left the facade, `LabWorkspaceState` mostly
applies/captures `SavedState` and wires persist / settings / palette.
Whether that leftover coordinator should exist is a later question. Do not
delete it as a line-count goal.

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

A query-side Channel barrier is only a debugging experiment. After this
baseline, if ordering comes back, put it **inside the worker**:

```
UI: post edit / completion / hover immediately
Worker: versioned / ordered LS session → Roslyn
```

Do not reopen HybridCache, `DropOldest` on LS deltas, or deleting the facade
in this pass.

### 8. Remaining Channel races — next

Compile / persist leftovers. Independent of the LS undo.

- [ ] `PersistenceQueue`: take coalesced `PersistKind` flags **and** waiters in
      one lock after the debounce. Today `TakeQueued()` then `TakeWaiters()`
      can complete a waiter whose flags have not run yet.
- [ ] `CompileCoreAsync`: do not `return` just because `Compiler.Loading`. The
      scheduler has already consumed the pulse and will complete waiters. Wait
      until idle while still current, or re-enqueue when loading finishes.
      `_compileInFlight` can go once that is the only gate.

### 9. Compilation cache key schema

Prefix the hashed key (or `ToCacheSlug`) with an explicit schema version so a
`CompiledAssembly` / serialization change does not serve structurally valid
stale IndexedDB or remote entries. Bumping the IDB database name already wipes
the browser; it does not version the shared HTTP cache. SHA-256 instead of
`XxHash128` is optional hardening for the public remote namespace, not a
substitute for a version prefix.

## Later (not now)

- [ ] Worker-side LS session: versioned `DocumentChanged` / queries, or an
      ordered queue **inside** the web worker. UI only posts. Do not put the
      unbounded round-trip Channel back in `LabLanguageServices`.
- [ ] Profile `LabCodeEditor` reconstructing the full source with
      `string.Concat` per Monaco change (O(document) per edit)
- [ ] `EnableSemanticHighlightingAsync` once globally — today each
      `LabCodeEditor` init loops every Monaco instance and `addAction`s again
- [ ] Native host (`IWorkerTransport` in-process; default
      `UnsupportedWorkerTransport.CreateWorker` throws)
- [ ] Document metadata Fluxor (`ActiveDocument` / template / open names)
      only with a real consumer
- [ ] `WorkerState` for `WorkerError` only if more than one UI surface needs it
- [ ] Server-host Redis `IDistributedCache` (not WASM HybridCache / IndexedDB)
- [ ] Compile Fluxor `CompileRequestedAction` → existing scheduler (session
      is already gated)
- [ ] Rename `OutputLoadCache` → `OutputSession` if the name still misleads
- [ ] Split compile-affecting options (`RazorToolchain` / `RazorStrategy`) from
      output-only prefs (`ShowOperations`, `FullIl`, …) **only after**
      `GetOutput` / lazy formatters take *current* prefs. Today those prefs
      live on `CompilationInput`, so `CanReuseLastCompile` fails and the next
      compile is a full Roslyn run. Fluxor-splitting first does not save work.
- [ ] Optional last-N in-process map on `ICompilationCache` (not HybridCache)
- [ ] `IAsyncDisposable` on Channel readers so Dispose waits for the loop
- [ ] Size-based IndexedDB eviction (today max 16 entries)
- [ ] Drop `LabWorkspaceState` entirely once it is only glue — decide then,
      do not pre-delete
