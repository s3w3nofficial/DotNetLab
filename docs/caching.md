# Compilation caching

Reuse compiled output so a reload, share-URL open, or second visitor does not
always wait on Roslyn. Three layers, in this order:

```
Compile / apply saved state
 ├─ TemplateCache          gzipped well-known C# / Razor / CSHTML
 └─ ICompilationCache      (skipped if Enable Caching is off)
      ├─ IndexedDB L1      origin-scoped, LRU 16
      └─ remote HTTP L2    vsinsertions.azurewebsites.net
           └─ miss → WorkerHost Compile → Store L1 then L2
```

`CompilationSession` compiles. The cache never compiles. A miss returns
`null`; errors are also `null`. Do **not** store misses.

`Enable Caching` (`PreferencesState`) gates Get/Store. Template hits skip
IndexedDB/HTTP even when caching is on.

## TemplateCache

`Infrastructure/Caching/Template/TemplateCache.cs`. Build-time gzipped
`CompiledAssembly` JSON for the three default templates
(`SavedState.CSharp` / `Razor` / `Cshtml`) when compiler versions are
built-in.

`TryApplyTemplateCache` runs first on `ApplySavedState`. If the sources match
a template but compilation prefs differ from default, the cached assembly is
still applied and the session is marked stale (UI can recompile).

Do not replace this with IndexedDB or HybridCache. The payloads are small and
must work offline on first paint.

## ICompilationCache

`Infrastructure/Caching/Compilation/`:

| Type | Role |
|---|---|
| `CompilationCache` | L1 then L2; per-key in-flight `Get` coalesces stampede |
| `IndexedDbCompilationCache` | L1 via `netLabCompileCache` in `lab-persist.js` |
| `RemoteCompilationCache` | L2 `POST …/api/cache/get/{key}` and `…/add/{key}` |
| `CompilationCacheKey` | `v{Schema}-{XxHash128(hex)}` |

`Schema` is currently `1`. Bump it when `CompiledAssembly` / Worker JSON
cannot safely reuse older rows. The IndexedDB **database** name
(`netlab-compile-v1`) is versioned separately and wipes the browser store on
bump. The key prefix versions the shared HTTP cache.

### Key

`SavedState.ToCacheSlug()` compresses `ToCacheKey()`, which keeps sources,
compiler versions, Razor toolchain, and compilation prefs, and drops the
selected input/output tabs and `SdkVersion`. `CompilationCacheKey.Create`
hashes that slug with `XxHash128` and prefixes `v1-`.

Format-only prefs (Full IL, Operations, …) are still on that slug today —
toggling them misses the cache. Splitting that is deferred (see
[`app-followups.md`](app-followups.md)).

### IndexedDB (L1)

JS (`lab-persist.js`):

- DB `netlab-compile-v1`, store `entries`, keyPath `key`
- Row: `{ key, json, timestamp, accessed }`
- Get updates `accessed`; put evicts oldest `accessed` when count > 16
- Missing IndexedDB / quota / parse errors → miss (native hosts no-op)

C# deserializes `json` with `WorkerJsonContext.CompiledAssembly`.

### Remote HTTP (L2)

`https://vsinsertions.azurewebsites.net/api/cache`. Not
`IDistributedCache`. Get uses the `X-Timestamp` header. Store `409 Conflict`
means the key already exists — expected, logged, ignored.

A remote hit is written through to IndexedDB.

### When Store runs

User compiles (`storeInCache: true`) after a successful
`WorkerInputMessage.Compile`. Template inputs are not stored again. Quiet
`ApplySavedState` compiles use `storeInCache: false` so a default template
open does not POST the same payload.

In-session reuse (`CanReuseLastCompile`) is `CompilationInput` equality on
`CompilationSession`, not this cache.

## What is not this cache

| Thing | Where |
|---|---|
| Output tab text / Monaco URIs | `OutputSession` (RAM, cleared on new compile) |
| Settings / output-tab order | `localStorage` via `netLabPrefs` |
| Share URL | hash slug (`LabUrlSync`) |
| HybridCache package | not used |
