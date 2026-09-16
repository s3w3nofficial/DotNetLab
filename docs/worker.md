# Worker offload

CPU-heavy compiler and language-service work is supposed to run off the UI.
`WorkerHost` is the only App entry point. The wire format is
`WorkerInputMessage` / `WorkerOutputMessage` JSON in `src/WorkerApi`.

## Where it actually runs

Chosen on first `SendAsync` (reload to change Background worker):

| Log | When |
|---|---|
| `LANGUAGE SERVICES EXECUTION: browser worker` | Browser, Background worker on, `IWorkerTransport.SupportsBackgroundWorker` |
| `LANGUAGE SERVICES EXECUTION: background .NET thread` | In-process and `ILabEnvironment.SupportsThreads` (`Task.Run`) |
| `LANGUAGE SERVICES EXECUTION: UI/foreground` | In-process, no real threads (browser WASM with worker off; `SupportsThreads` is false) |

Browser WASM has **no** `WasmEnableThreads`. Turning the worker off runs
Roslyn on the UI thread (useful for breakpoints; see README). Native hosts
cannot create a web worker (`UnsupportedWorkerTransport`) and use in-process
`WorkerServices`.

The worker process is `src/WorkerWebAssembly` loading `src/Worker` →
`CompilerProxy` / `LanguageServices`. App never calls Roslyn directly.

`Cancel` is a separate message (`WorkerInputMessage.Cancel`). Do not put it
on a Channel behind the request it aborts.

## What is offloaded

Every `WorkerInputMessage` handler in `src/Worker/Executor.cs`:

### Compiler / SDK

| Message | Work |
|---|---|
| `Compile` | `CompilerProxy.CompileAsync`; optionally notify LS |
| `GetOutput` | Compile (reuse) + lazy tab text (IL, Asm, Tree, Run, …) |
| `FormatCode` | Roslyn format |
| `UseCompilerVersion` | Download / switch SDK, Roslyn, or Razor build |
| `GetCompilerDependencyInfo` | Package metadata for the current compiler |
| `GetSdkVersions` / `GetSdkInfo` | Version lists and SDK details |
| `TryGetSubRepoCommitHash` | Map a mono-repo commit to a sub-repo |

### Language services

| Message | Work |
|---|---|
| `OnDidChangeWorkspace` / `OnDidChangeModelContent` | Apply Monaco edits to the AdhocWorkspace |
| `OnCachedCompilationLoaded` | Seed LS from a cached assembly |
| `ProvideCompletionItems` / `ResolveCompletionItem` | Completions |
| `ProvideSemanticTokens` | C# semantic highlighting |
| `ProvideCodeActions` | Quick fixes |
| `ProvideHover` / `ProvideSignatureHelp` | Hover and signatures |
| `GetDiagnostics` | Live markers |

App currently only **requests** cheap completion triggers (`.` `(` `<` `#`
`[`). Identifier / space / Enter lists stay off until Compiler shrinks the
JSON ([`app-followups.md`](app-followups.md) Deferred).

### Process

| Message | Work |
|---|---|
| `Ping` | Memory sample; keep-alive |
| `Cancel` | Cancel the CTS for another message id |

GC dump is a side channel (`collect-gc-dump`), not a `WorkerInputMessage`.

## What stays on the UI

- Monaco, Fluxor, dialogs, tab drag-and-drop, URL hash
- IndexedDB / `localStorage` (JS on the UI thread)
- `OutputSession` applying worker text into Monaco
- `LabLanguageServices` debounce, mutation fence, and which LS APIs to call
- Remote cache HTTP from the App `HttpClient` (not the worker)

A browser worker still **posts JSON back** to the UI. Huge completion lists
were parsed on the UI thread; that is why App filters them.

## Protocol notes

- Message ids are monotonic for the `WorkerHost` lifetime (not reset on
  recreate). Late replies from a previous worker are dropped via `_epoch`.
- `SendAsync` can `WaitAsync` the caller's token so the UI unblocks even if
  the worker is still computing; the worker may still finish and post a
  result that is then unmatched.
- In-process requests are not serialized: `Cancel` must overlap the handler
  it aborts.
