# Tab drag and drop

Source and output panes each host an `EditorGroups` instance. Tabs can be
reordered, moved between groups in the **same pane**, or dropped on a pane
edge to split. Error List and other pinned tabs are not draggable.

There is no drag between source and output (`data-lab-pane` must match).

## Pieces

| Piece | Role |
|---|---|
| `EditorGroups.razor` | HTML5 drag on user tabs; drop on tab, tab list, or group body |
| `EditorDragState` | Scoped C# copy of the in-flight tab (`Kind`/`Pane`/`Group`/`Value`) |
| `wwwroot/js/lab-layout.js` | Capture, hover chrome, split zone hit-test |

Blazor `DragEventArgs` often lose `dataTransfer` across the JS interop
boundary, so JS keeps `window.netLabDrag` as the source of truth.
`ReadDragAsync` prefers `netLabLayout.currentDrag()` and falls back to
`EditorDragState` / local fields.

## Start

1. User-tab `<button draggable="true">` with `data-lab-tab`, `data-lab-group`,
   `data-lab-pane`.
2. `mousedown` with `event.detail > 1` sets `draggable = false` so double-click
   rename is not a drag.
3. Capture-phase `dragstart` on `document` (JS) writes `window.netLabDrag`,
   marks `.lab-tab-dragging`, `setData("text/plain", …)` for Firefox, and
   `beginDrop(pane)` (`.lab-drop-ready` on matching pane bodies).
4. Blazor `StartTabDrag` mirrors that onto `EditorDragState`.

Pinned tabs return immediately from `StartTabDrag`.

## Hover

Capture-phase `dragover`:

- Over `.lab-drop-overlay` (editor body): `hoverDrop` — 25% edge →
  left/right/top/bottom, else center. CSS class on `.lab-drop-zone`.
- Over `.lab-tab` / `.lab-tab-list`: `hoverTab` — `.drop-before` /
  `.drop-after` from pointer vs tab midpoint. `netLabTabDropAfter` is read
  on drop.

`preventDefault` is required or the browser will not fire `drop`.

## Drop

Blazor handlers (after JS `preventDefault` on drop):

| Target | Action |
|---|---|
| Another tab | `MoveTab` to that index (`+1` if `tabDropAfter`) |
| Empty / rest of the tab list | Append among user tabs |
| Group body, zone center | Append to that group |
| Group body, edge | `SplitTab` — new group; orientation vertical for top/bottom, horizontal for left/right |

`MoveTab` / `SplitTab` only mutate `_groups` in that pane. Empty groups are
removed. `TabsChanged` / `ActiveChanged` notify `LabWorkspace`.

`dragend` / `EndDrag` clears `netLabDrag`, C# state, and hover classes.

## Splitter (not HTML5 drag)

The source/output **pane** split is pointer capture on the splitter:
`netLabLayout.beginSplit` / `endSplit`, clamped 25–75%, stored as
`--pane-split` and Fluxor `WorkspaceState.Split`. Independent of tab DnD.
