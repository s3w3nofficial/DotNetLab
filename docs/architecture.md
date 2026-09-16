# Architecture

Current web app: Blazor WebAssembly `src/App`, hosted by `src/WebAssembly`
(deployed) or `src/Server` (dev static files only — not a Blazor circuit).

```
UI (Monaco, Fluent, Fluxor)
        │
        ▼
LabWorkspaceState          saved-state / orchestration facade
        │
   ┌────┼─────────────┐
   ▼    ▼             ▼
Docs  Compilation   Outputs
      Session        Session
        │
        ▼
    WorkerHost  ── JSON ──►  browser worker or in-process WorkerServices
```

Do **not** Fluxor `LabWorkspaceState`, Monaco handles, worker handles, or
`CompiledAssembly`. Follow-ups and “do not”s: [`app-followups.md`](app-followups.md).

## Hosts

| Project | Role |
|---|---|
| `src/WebAssembly` | Production WASM host; registers `BrowserWorkerTransport` |
| `src/Server` | `UseBlazorFrameworkFiles` + `index.html` for local run |
| `src/WorkerWebAssembly` | Separate WASM for the background worker |
| Native/store | Later; in-process worker (`SupportsBackgroundWorker` false) |

## Fluxor vs sessions

Fluxor holds serializable UI facts. Sessions hold live resources.

| Fluxor | Runtime |
|---|---|
| `CompilerState` | `LabDocuments` (file text / URIs) |
| `CompilationState` (Running / Stale) | `CompilationSession` |
| `CompilationOptionsState` | `OutputSession` / `OutputTabLayout` |
| `OutputState` (`ActiveOutput`) | `LabLanguageServices` |
| `PreferencesState` | `WorkerHost` / `EditorCursor` |
| `WorkspaceState` (`Split`) | |
| `DocumentMetadataState` | |
| `UpdateState` | |

## Compile path

Toolbar / palette / Ctrl+S dispatch `CompileRequestedAction`. The effect
calls `CompilationSession.CompileAsync`. `CompilationScheduler` is a bounded
1 / `DropOldest` Channel: latest-wins, cancels in-flight Roslyn when a newer
request arrives. `storeInCache` is coalesced so DropOldest cannot lose a
user compile that should POST to the cache.

Quiet `ApplySavedState` still calls `CompileAsync` directly (not the Fluxor
action) so it does not look like a user click.

## Persistence (not compilation cache)

`PersistenceQueue` (50ms debounce, bounded 1 / DropOldest) writes:

| Kind | Destination |
|---|---|
| URL | `LabUrlSync` → `#` slug (`Compressor.Compress(SavedState)`), well-known `#csharp` / `#razor` / `#cshtml` |
| Settings | `localStorage` `netlab-settings` (`netLabPrefs`) |
| Output tabs | `localStorage` `netlab-output-tabs` |

`_suppressUrlPersist` skips URL writes while applying a hash. Invalid slugs
fall back to the C# template.

Gist “create” copies a snapshot and opens gist.github.com; it is not the
compile cache.

## Output tabs

`OutputSession` keeps per-tab text and Monaco model URIs **in memory** for
the current `CompiledAssembly`. Switching IL → Asm may `GetOutput` on the
worker (lazy). Clearing happens on a new compile generation. This is not
IndexedDB.

Error List can temporarily replace an empty output tab after a failed
compile; it stays pinned in the tab bar.

## Language services (UI side)

`LabLanguageServices` registers Monaco providers and posts to `WorkerHost`.
Document mutations are posted immediately (not Channelled). Completions for
`.` `(` `<` `#` `[` wait on the latest mutation Task. Identifier completions
are deferred until Compiler shrinks the list — see
[`app-followups.md`](app-followups.md) and [`worker.md`](worker.md).

## Related

- [`caching.md`](caching.md)
- [`worker.md`](worker.md)
- [`drag-and-drop.md`](drag-and-drop.md)
