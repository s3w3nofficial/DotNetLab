# App internals

How the current `src/App` stack is put together (hosted by `src/WebAssembly`
/ `src/Server`). Remaining work is in [`app-followups.md`](app-followups.md).
Native/store apps: [`native-apps.md`](native-apps.md).

| Doc | Covers |
|---|---|
| [Architecture](architecture.md) | Hosts, Fluxor vs sessions, URL, prefs, compile/persist schedulers |
| [Caching](caching.md) | Template snapshots, IndexedDB L1, remote HTTP L2 |
| [Worker](worker.md) | What runs on the worker vs the UI |
| [Drag and drop](drag-and-drop.md) | Source/output tab reorder and split |
