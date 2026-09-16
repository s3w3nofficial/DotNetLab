window.netLabLayout = {
    getRect: function (el) {
        const rect = el.getBoundingClientRect();
        return { top: rect.top, left: rect.left, width: rect.width, height: rect.height };
    },
    capturePointer: function (el, pointerId) {
        el.setPointerCapture(pointerId);
    },
    releasePointer: function (el, pointerId) {
        if (el.hasPointerCapture(pointerId)) {
            el.releasePointerCapture(pointerId);
        }
    },
    setSplit: function (el, percent) {
        el.style.setProperty("--pane-split", `${percent}%`);
        el.classList.toggle("resizing", true);
    },
    beginSplit: function (workspace, splitter, pointerId, clientX, clientY) {
        if (!workspace || !splitter) {
            return;
        }

        const apply = (x, y) => {
            const rect = workspace.getBoundingClientRect();
            if (rect.width <= 0 || rect.height <= 0) {
                return;
            }

            const stacked = workspace.classList.contains("stacked");
            let percent = stacked
                ? ((y - rect.top) / rect.height) * 100
                : ((x - rect.left) / rect.width) * 100;
            percent = Math.min(75, Math.max(25, percent));
            workspace.style.setProperty("--pane-split", `${percent}%`);
            workspace.classList.add("resizing");
            workspace._labSplit = percent;
        };

        const onMove = (e) => {
            if ((e.buttons & 1) === 0) {
                return;
            }

            apply(e.clientX, e.clientY);
        };

        splitter._labSplitMove = onMove;
        splitter._labSplitPointerId = pointerId;
        try {
            splitter.setPointerCapture(pointerId);
        } catch {
        }

        splitter.addEventListener("pointermove", onMove);
        apply(clientX, clientY);
    },
    endSplit: function (workspace, splitter) {
        if (splitter && splitter._labSplitMove) {
            splitter.removeEventListener("pointermove", splitter._labSplitMove);
            const pointerId = splitter._labSplitPointerId;
            splitter._labSplitMove = null;
            splitter._labSplitPointerId = null;
            try {
                if (pointerId != null && splitter.hasPointerCapture(pointerId)) {
                    splitter.releasePointerCapture(pointerId);
                }
            } catch {
            }
        }

        if (workspace) {
            workspace.classList.remove("resizing");
            return typeof workspace._labSplit === "number" ? workspace._labSplit : null;
        }

        return null;
    },
    zoneFromPoint: function (el, clientX, clientY) {
        if (!el) {
            return "center";
        }

        const rect = el.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) {
            return "center";
        }

        const x = (clientX - rect.left) / rect.width;
        const y = (clientY - rect.top) / rect.height;
        const edge = 0.25;
        if (x < edge) {
            return "left";
        }

        if (x > 1 - edge) {
            return "right";
        }

        if (y < edge) {
            return "top";
        }

        if (y > 1 - edge) {
            return "bottom";
        }

        return "center";
    },
    dropZone: function (clientX, clientY) {
        const bodies = document.querySelectorAll(".lab-group-body");
        for (const el of bodies) {
            const rect = el.getBoundingClientRect();
            if (clientX >= rect.left && clientX <= rect.right && clientY >= rect.top && clientY <= rect.bottom) {
                return window.netLabLayout.zoneFromPoint(el, clientX, clientY);
            }
        }

        const hit = document.elementFromPoint(clientX, clientY);
        return window.netLabLayout.zoneFromPoint(hit && hit.closest(".lab-group-body"), clientX, clientY);
    },
    currentDrag: function () {
        return window.netLabDrag || {};
    },
    beginDrop: function (pane) {
        document.querySelectorAll(".lab-group-body").forEach(function (el) {
            const host = el.closest("[data-lab-pane]");
            if (!pane || (host && host.getAttribute("data-lab-pane") === pane)) {
                el.classList.add("lab-drop-ready");
            }
        });
    },
    hoverDrop: function (clientX, clientY) {
        window.netLabLayout.clearTabDrop();
        const hit = document.elementFromPoint(clientX, clientY);
        const body = hit && hit.closest(".lab-group-body");
        const zone = window.netLabLayout.zoneFromPoint(body, clientX, clientY);
        document.querySelectorAll(".lab-drop-zone").forEach(function (el) {
            const active = body && body.contains(el);
            el.className = active ? "lab-drop-zone " + zone : "lab-drop-zone";
        });
        return zone;
    },
    clearTabDrop: function () {
        document.querySelectorAll(".lab-tab.drop-before, .lab-tab.drop-after").forEach(function (el) {
            el.classList.remove("drop-before", "drop-after");
        });
    },
    hoverTab: function (clientX, clientY) {
        window.netLabLayout.clearTabDrop();
        document.querySelectorAll(".lab-drop-zone").forEach(function (el) {
            el.className = "lab-drop-zone";
        });

        const drag = window.netLabDrag;
        if (!drag || drag.kind !== "tab") {
            return;
        }

        const hit = document.elementFromPoint(clientX, clientY);
        const list = hit && hit.closest(".lab-tab-list");
        if (!list) {
            return;
        }

        const tabs = [...list.querySelectorAll(".lab-tab")].filter(function (tab) {
            return tab.getAttribute("data-lab-pane") === drag.pane;
        });
        if (tabs.length === 0) {
            return;
        }

        let target = hit.closest(".lab-tab");
        if (target && target.getAttribute("data-lab-pane") !== drag.pane) {
            return;
        }

        if (!target) {
            const last = tabs[tabs.length - 1];
            if (last.getAttribute("data-lab-tab") !== drag.value) {
                last.classList.add("drop-after");
            }

            window.netLabTabDropAfter = true;
            return;
        }

        if (target.getAttribute("data-lab-tab") === drag.value) {
            return;
        }

        const rect = target.getBoundingClientRect();
        const after = clientX > rect.left + rect.width / 2;
        window.netLabTabDropAfter = after;
        target.classList.add(after ? "drop-after" : "drop-before");
        const neighbor = after ? target.nextElementSibling : target.previousElementSibling;
        if (neighbor && neighbor.classList.contains("lab-tab") && neighbor.getAttribute("data-lab-tab") !== drag.value) {
            neighbor.classList.add(after ? "drop-before" : "drop-after");
        }
    },
    tabDropAfter: function () {
        return !!window.netLabTabDropAfter;
    },
    endDrop: function () {
        window.netLabDrag = null;
        window.netLabTabDropAfter = false;
        window.netLabLayout.clearVisual();
    },
    clearVisual: function () {
        document.querySelectorAll(".lab-tab.lab-tab-dragging").forEach(function (el) {
            el.classList.remove("lab-tab-dragging");
        });
        window.netLabLayout.clearTabDrop();
        document.querySelectorAll(".lab-group-body").forEach(function (el) {
            el.classList.remove("lab-drop-ready");
        });
        document.querySelectorAll(".lab-drop-zone").forEach(function (el) {
            el.className = "lab-drop-zone";
        });
    }
};

if (!window.netLabDropBound) {
    window.netLabDropBound = true;
    document.addEventListener("dragstart", function (e) {
        const tab = e.target.closest("[data-lab-tab]");
        if (!tab) {
            return;
        }

        const drag = {
            kind: "tab",
            pane: tab.getAttribute("data-lab-pane"),
            group: tab.getAttribute("data-lab-group"),
            value: tab.getAttribute("data-lab-tab")
        };

        window.netLabDrag = drag;
        tab.classList.add("lab-tab-dragging");
        try {
            e.dataTransfer.effectAllowed = "move";
            e.dataTransfer.setData("text/plain", drag.value || "tab");
        } catch {
        }

        window.netLabLayout.beginDrop(drag.pane);
    }, true);

    document.addEventListener("dragover", function (e) {
        if (!window.netLabDrag) {
            return;
        }

        const overlay = e.target.closest(".lab-drop-overlay");
        if (overlay) {
            e.preventDefault();
            e.dataTransfer.dropEffect = "move";
            window.netLabLayout.hoverDrop(e.clientX, e.clientY);
            return;
        }

        if (e.target.closest(".lab-tab, .lab-tab-list")) {
            e.preventDefault();
            e.dataTransfer.dropEffect = "move";
            window.netLabLayout.hoverTab(e.clientX, e.clientY);
        }
    }, true);

    document.addEventListener("drop", function (e) {
        if (e.target.closest(".lab-drop-overlay, .lab-tab, .lab-tab-list")) {
            e.preventDefault();
        }
    }, true);

    document.addEventListener("dragend", function () {
        window.netLabLayout.clearVisual();
    }, true);
}

window.netLabPrefs = {
    outputTabsKey: "netlab-output-tabs",
    settingsKey: "netlab-settings",
    readOutputTabs: function () {
        try {
            return localStorage.getItem(this.outputTabsKey) || "";
        } catch {
            return "";
        }
    },
    persistOutputTabs: function (json) {
        try {
            if (!json) {
                localStorage.removeItem(this.outputTabsKey);
                return;
            }

            localStorage.setItem(this.outputTabsKey, json);
        } catch {
        }
    },
    readSettings: function () {
        try {
            return localStorage.getItem(this.settingsKey) || "";
        } catch {
            return "";
        }
    },
    persistSettings: function (json) {
        try {
            if (!json) {
                localStorage.removeItem(this.settingsKey);
                return;
            }

            localStorage.setItem(this.settingsKey, json);
        } catch {
        }
    }
};

window.netLabCompileCache = {
    dbName: "netlab-compile-v1",
    storeName: "entries",
    maxEntries: 16,
    _db: null,
    open: function () {
        if (this._db) {
            return Promise.resolve(this._db);
        }

        if (!window.indexedDB) {
            return Promise.reject(new Error("indexedDB unavailable"));
        }

        const self = this;
        return new Promise(function (resolve, reject) {
            const request = indexedDB.open(self.dbName, 1);
            request.onupgradeneeded = function () {
                const db = request.result;
                if (!db.objectStoreNames.contains(self.storeName)) {
                    const store = db.createObjectStore(self.storeName, { keyPath: "key" });
                    store.createIndex("accessed", "accessed");
                }
            };
            request.onsuccess = function () {
                self._db = request.result;
                self._db.onclose = function () {
                    self._db = null;
                };
                resolve(self._db);
            };
            request.onerror = function () {
                reject(request.error);
            };
        });
    },
    request: function (req) {
        return new Promise(function (resolve, reject) {
            req.onsuccess = function () {
                resolve(req.result);
            };
            req.onerror = function () {
                reject(req.error);
            };
        });
    },
    get: async function (key) {
        try {
            const db = await this.open();
            const read = db.transaction(this.storeName, "readonly");
            const row = await this.request(read.objectStore(this.storeName).get(key));
            if (!row || !row.json) {
                return "";
            }

            const write = db.transaction(this.storeName, "readwrite");
            row.accessed = Date.now();
            write.objectStore(this.storeName).put(row);
            return JSON.stringify({ output: row.json, timestamp: row.timestamp });
        } catch {
            this._db = null;
            return "";
        }
    },
    put: async function (key, json, timestamp) {
        try {
            const db = await this.open();
            const write = db.transaction(this.storeName, "readwrite");
            write.objectStore(this.storeName).put({
                key: key,
                json: json,
                timestamp: timestamp,
                accessed: Date.now()
            });
            await this.complete(write);

            const countTx = db.transaction(this.storeName, "readonly");
            const count = await this.request(countTx.objectStore(this.storeName).count());
            if (count <= this.maxEntries) {
                return;
            }

            let extra = count - this.maxEntries;
            const evict = db.transaction(this.storeName, "readwrite");
            const index = evict.objectStore(this.storeName).index("accessed");
            await new Promise(function (resolve, reject) {
                const cursor = index.openCursor();
                cursor.onsuccess = function (e) {
                    const current = e.target.result;
                    if (!current || extra <= 0) {
                        resolve();
                        return;
                    }

                    current.delete();
                    extra--;
                    current.continue();
                };
                cursor.onerror = function () {
                    reject(cursor.error);
                };
            });
        } catch {
            this._db = null;
        }
    },
    complete: function (tx) {
        return new Promise(function (resolve, reject) {
            tx.oncomplete = function () {
                resolve();
            };
            tx.onerror = function () {
                reject(tx.error);
            };
            tx.onabort = function () {
                reject(tx.error);
            };
        });
    }
};

window.netLabMemory = {
    collectAndDownloadGcDump: async function () {
        const runtime = globalThis.getDotnetRuntime?.(0);
        if (!runtime?.collectGcDump) {
            return;
        }

        const result = await runtime.collectGcDump({ skipDownload: true });
        const blob = new Blob(result, { type: "application/octet-stream" });
        const blobUrl = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.download = `app.trace.${Date.now()}.nettrace`;
        link.href = blobUrl;
        document.body.appendChild(link);
        link.dispatchEvent(new MouseEvent("click", {
            bubbles: true, cancelable: true, view: window,
        }));
        link.remove();
        URL.revokeObjectURL(blobUrl);
    }
};

window.netLabUrl = {
    hash: function () {
        return (window.location.hash || "").replace(/^#/, "");
    }
};

window.netLabTheme = {
    storageKey: "netlab-theme",
    _media: null,
    _mediaHandler: null,
    readPreference: function () {
        try {
            const value = localStorage.getItem(this.storageKey);
            if (value === "light" || value === "dark" || value === "system") {
                return value;
            }
        } catch {
        }

        return "dark";
    },
    persist: function (preference) {
        try {
            localStorage.setItem(this.storageKey, preference === "light" || preference === "dark" || preference === "system"
                ? preference
                : "dark");
        } catch {
        }
    },
    resolveDark: function (preference) {
        if (preference === "light") {
            return false;
        }

        if (preference === "dark") {
            return true;
        }

        return !window.matchMedia || window.matchMedia("(prefers-color-scheme: dark)").matches;
    },
    applyDocument: function (isDark) {
        const theme = isDark ? "dark" : "light";
        document.documentElement.setAttribute("data-theme", theme);
        document.documentElement.setAttribute("theme", theme);
        document.documentElement.style.colorScheme = theme;
    },
    listenSystem: function (dotNet) {
        this.stopListening();
        if (!window.matchMedia) {
            return;
        }

        this._media = window.matchMedia("(prefers-color-scheme: dark)");
        this._mediaHandler = function (event) {
            dotNet.invokeMethodAsync("OnSystemThemeChanged", event.matches);
        };
        this._media.addEventListener("change", this._mediaHandler);
    },
    stopListening: function () {
        if (this._media && this._mediaHandler) {
            this._media.removeEventListener("change", this._mediaHandler);
        }

        this._media = null;
        this._mediaHandler = null;
    }
};

window.netLabTheme.applyDocument(window.netLabTheme.resolveDark(window.netLabTheme.readPreference()));

window.netLabMonaco = {
    defineTheme: function () {
        if (!window.monaco || !window.monaco.editor) {
            return;
        }

        window.monaco.editor.defineTheme("netlab-dark", {
            base: "vs-dark",
            inherit: true,
            rules: [
                { token: "keyword", foreground: "c8a4f7" },
                { token: "type", foreground: "9fd7ff" },
                { token: "comment", foreground: "7a7189", fontStyle: "italic" },
                { token: "string", foreground: "b6e3a7" },
                { token: "number", foreground: "f0c987" },
                { token: "tag", foreground: "9fd7ff" },
                { token: "attribute.name", foreground: "c8a4f7" }
            ],
            colors: {
                "editor.background": "#150F1D",
                "editor.foreground": "#EDE8F5",
                "editorLineNumber.foreground": "#5A4E6B",
                "editorLineNumber.activeForeground": "#C8A4F7",
                "editor.selectionBackground": "#3B2C52",
                "editor.lineHighlightBackground": "#1D1526",
                "editorGutter.background": "#150F1D",
                "editorWidget.background": "#251C31",
                "editorWidget.foreground": "#EDE8F5",
                "editorWidget.border": "#3B2C52",
                "editorIndentGuide.background1": "#2A2135",
                "menu.background": "#251C31",
                "menu.foreground": "#EDE8F5",
                "menu.selectionBackground": "#3B2C52",
                "menu.selectionForeground": "#F7F1FC",
                "menu.separatorBackground": "#3B2C52",
                "menu.border": "#3B2C52",
                "widget.shadow": "#150F1D8C",
                "widget.border": "#3B2C52",
                "list.hoverBackground": "#3B2C52",
                "list.activeSelectionBackground": "#3B2C52",
                "list.activeSelectionForeground": "#F7F1FC",
                "dropdown.background": "#251C31",
                "dropdown.foreground": "#EDE8F5",
                "dropdown.border": "#3B2C52",
                "quickInput.background": "#251C31",
                "quickInput.foreground": "#EDE8F5"
            }
        });

        window.monaco.editor.defineTheme("netlab-light", {
            base: "vs",
            inherit: true,
            rules: [
                { token: "keyword", foreground: "6e4a9e" },
                { token: "type", foreground: "2a6f9c" },
                { token: "comment", foreground: "6b5f78", fontStyle: "italic" },
                { token: "string", foreground: "2f7a4a" },
                { token: "number", foreground: "b07a20" },
                { token: "tag", foreground: "2a6f9c" },
                { token: "attribute.name", foreground: "6e4a9e" }
            ],
            colors: {
                "editor.background": "#F4EEF8",
                "editor.foreground": "#1D1526",
                "editorLineNumber.foreground": "#8B8099",
                "editorLineNumber.activeForeground": "#6E4A9E",
                "editor.selectionBackground": "#D4C4E8",
                "editor.lineHighlightBackground": "#FBF8FD",
                "editorGutter.background": "#F4EEF8",
                "editorWidget.background": "#FFFFFF",
                "editorWidget.foreground": "#1D1526",
                "editorWidget.border": "#D4C4E8",
                "editorIndentGuide.background1": "#E4D6F0",
                "menu.background": "#FFFFFF",
                "menu.foreground": "#1D1526",
                "menu.selectionBackground": "#D4C4E8",
                "menu.selectionForeground": "#1D1526",
                "menu.separatorBackground": "#D4C4E8",
                "menu.border": "#D4C4E8",
                "widget.shadow": "#1D152629",
                "widget.border": "#D4C4E8",
                "list.hoverBackground": "#D4C4E8",
                "list.activeSelectionBackground": "#D4C4E8",
                "list.activeSelectionForeground": "#1D1526",
                "dropdown.background": "#FFFFFF",
                "dropdown.foreground": "#1D1526",
                "dropdown.border": "#D4C4E8",
                "quickInput.background": "#FFFFFF",
                "quickInput.foreground": "#1D1526"
            }
        });
    },
    tokens: function () {
        const cs = getComputedStyle(document.documentElement);
        const read = function (name, fallback) {
            const value = cs.getPropertyValue(name).trim();
            return value || fallback;
        };

        return {
            raised: read("--lab-panel-raised", "#251C31"),
            foreground: read("--lab-foreground", "#EDE8F5"),
            hairline: read("--lab-hairline", "#3B2C52"),
            muted: read("--lab-muted", "#8B8099"),
            shadow: read("--lab-shadow", "0 12px 32px rgba(21, 15, 29, 0.55)")
        };
    },
    applyTheme: function (isDark) {
        window.netLabMonaco.defineTheme();
        if (!window.monaco || !window.monaco.editor) {
            return;
        }

        window.monaco.editor.setTheme(isDark ? "netlab-dark" : "netlab-light");
        window.netLabMonaco.styleMenus();
    },
    styleMenus: function () {
        const t = window.netLabMonaco.tokens();
        const css = `
:host {
    font-family: "Manrope", sans-serif !important;
    --vscode-menu-background: ${t.raised} !important;
    --vscode-menu-foreground: ${t.foreground} !important;
    --vscode-menu-selectionBackground: ${t.hairline} !important;
    --vscode-menu-selectionForeground: ${t.foreground} !important;
    --vscode-menu-separatorBackground: ${t.hairline} !important;
    --vscode-menu-border: ${t.hairline} !important;
    --vscode-widget-shadow: ${t.shadow} !important;
    --vscode-widget-border: ${t.hairline} !important;
}
.monaco-menu {
    font-family: "Manrope", sans-serif;
    background: ${t.raised} !important;
    color: ${t.foreground} !important;
    border: 1px solid ${t.hairline} !important;
    border-radius: 0 !important;
    overflow: hidden;
    box-shadow: ${t.shadow} !important;
}
.monaco-menu .action-label {
    font-family: "Manrope", sans-serif;
}
.monaco-menu .keybinding {
    font-family: "JetBrains Mono", Consolas, monospace !important;
    color: ${t.muted} !important;
    opacity: 1 !important;
}`;

        document.querySelectorAll(".shadow-root-host").forEach(function (host) {
            const shadow = host.shadowRoot;
            if (!shadow) {
                return;
            }

            let style = shadow.getElementById("netlab-menu-style");
            if (!style) {
                style = document.createElement("style");
                style.id = "netlab-menu-style";
                shadow.appendChild(style);
            }

            style.textContent = css;
        });
    }
};

(function applyMonacoFromDocument() {
    if (!window.monaco || !window.monaco.editor) {
        return;
    }

    window.netLabMonaco.applyTheme(document.documentElement.getAttribute("data-theme") !== "light");
})();

(function suppressBrowserSave() {
    window.addEventListener("keydown", function (event) {
        if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey) {
            return;
        }

        if (event.key === "s" || event.key === "S" || event.code === "KeyS") {
            event.preventDefault();
        }
    }, true);
})();

window.netLabPalette = {
    _ref: null,
    _handler: null,
    isMac: function () {
        const platform = navigator.platform || "";
        const ua = navigator.userAgent || "";
        return /Mac|iPhone|iPad|iPod/.test(platform) || /Mac OS X/.test(ua);
    },
    bind: function (dotNetRef) {
        this.unbind();
        this._ref = dotNetRef;
        this._handler = (event) => this.onKeyDown(event);
        window.addEventListener("keydown", this._handler, true);
    },
    unbind: function () {
        if (this._handler) {
            window.removeEventListener("keydown", this._handler, true);
            this._handler = null;
        }

        this._ref = null;
    },
    show: function (el) {
        if (!el) {
            return;
        }

        if (typeof el.showPopover === "function") {
            try {
                el.showPopover();
            } catch (e) {
            }

            return;
        }

        el.setAttribute("data-open", "true");
    },
    hide: function (el) {
        if (!el) {
            return;
        }

        if (typeof el.hidePopover === "function") {
            try {
                if (el.matches && el.matches(":popover-open")) {
                    el.hidePopover();
                }
            } catch (e) {
            }

            return;
        }

        el.removeAttribute("data-open");
    },
    onKeyDown: function (event) {
        const target = event.target;
        if (target && target.classList && target.classList.contains("lab-palette-search")) {
            if (event.key === "ArrowDown" || event.key === "ArrowUp" || event.key === "Enter" || event.key === "Escape") {
                event.preventDefault();
            }
        }

        if (!this._ref || event.repeat) {
            return;
        }

        const host = document.getElementById("lab-palette-host");
        const paletteOpen = !!(host && ((host.matches && host.matches(":popover-open")) || host.getAttribute("data-open") === "true"));
        if (paletteOpen && event.key === "Escape") {
            event.preventDefault();
            event.stopPropagation();
            this._ref.invokeMethodAsync("HideFromKeyboard");
            return;
        }

        const isP = event.key === "p" || event.key === "P" || event.code === "KeyP";
        if ((event.ctrlKey || event.metaKey) && event.shiftKey && !event.altKey && isP) {
            event.preventDefault();
            event.stopPropagation();
            this._ref.invokeMethodAsync("ToggleFromKeyboard");
            return;
        }

        const isZ = event.key === "z" || event.key === "Z" || event.code === "KeyZ";
        if (event.altKey && !event.ctrlKey && !event.metaKey && !event.shiftKey && isZ) {
            event.preventDefault();
            event.stopPropagation();
            this._ref.invokeMethodAsync("ToggleWordWrapFromKeyboard");
        }
    }
};

(function bindDialogLightDismiss() {
    // Modal <dialog> is in the top layer, so document capture often never sees the click.
    // Fluent only hides when click.target === the inner <dialog>; overlay hits that node
    // (or the host after retargeting), not fluent-dialog-body.
    function bind(host) {
        if (!host || host.localName !== "fluent-dialog" || host._labLightDismiss) {
            return;
        }

        host._labLightDismiss = true;
        host.addEventListener("pointerdown", function (event) {
            if (host.type === "alert") {
                return;
            }

            const path = event.composedPath();
            if (path.some(function (node) { return node && node.localName === "fluent-dialog-body"; })) {
                return;
            }

            if (typeof host.hide === "function") {
                host.hide();
            }
        });
    }

    function scan(root) {
        if (!root) {
            return;
        }

        if (root.localName === "fluent-dialog") {
            bind(root);
        }

        if (root.querySelectorAll) {
            root.querySelectorAll("fluent-dialog").forEach(bind);
        }
    }

    scan(document);
    new MutationObserver(function (mutations) {
        for (const mutation of mutations) {
            for (const node of mutation.addedNodes) {
                scan(node);
            }
        }
    }).observe(document.documentElement, { childList: true, subtree: true });
})();

(function watchMonacoMenus() {
    const apply = () => window.netLabMonaco?.styleMenus?.();
    const observer = new MutationObserver(apply);
    observer.observe(document.documentElement, { childList: true, subtree: true });
    document.addEventListener("contextmenu", () => setTimeout(apply, 0), true);
})();

(function bindFluentMenuAnchors() {
    function triggerAnchorName(trigger) {
        return trigger.style.anchorName
            || trigger.style.getPropertyValue("anchor-name")
            || getComputedStyle(trigger).anchorName
            || "";
    }

    function apply(menu) {
        if (!menu || menu.localName !== "fluent-menu") {
            return;
        }

        const trigger = menu.querySelector("fluent-menu-button");
        const list = menu.querySelector("fluent-menu-list");
        if (!trigger || !list) {
            return;
        }

        // FluentTooltip writes a unique inline anchor-name on the trigger.
        // The menu list still targets --menu-trigger, so the popover falls back to 0,0.
        // Leave default --menu-trigger alone — that name is shared and must stay scoped by Fluent.
        const name = triggerAnchorName(trigger);
        if (!name || name === "none" || name === "--menu-trigger") {
            return;
        }

        if (list.style.positionAnchor !== name) {
            list.style.setProperty("position-anchor", name);
        }
    }

    function pinToTrigger(list) {
        const menu = list.closest("fluent-menu");
        const trigger = menu?.querySelector("fluent-menu-button");
        if (!trigger) {
            return;
        }

        const br = trigger.getBoundingClientRect();
        const lr = list.getBoundingClientRect();
        if (lr.width === 0 || (lr.x >= 2 && lr.y >= 2)) {
            return;
        }

        list.style.setProperty("position", "fixed", "important");
        list.style.setProperty("inset", "auto", "important");
        list.style.setProperty("top", `${br.bottom}px`, "important");
        list.style.setProperty("left", `${br.left}px`, "important");
    }

    function clearPin(list) {
        ["position", "inset", "top", "left"].forEach((prop) => list.style.removeProperty(prop));
    }

    function menuFromEvent(event) {
        const target = event.target;
        if (target?.closest) {
            const fromTarget = target.closest("fluent-menu")
                || target.closest("fluent-menu-button")?.closest("fluent-menu");
            if (fromTarget) {
                return fromTarget;
            }
        }

        for (const node of event.composedPath()) {
            if (node?.localName === "fluent-menu") {
                return node;
            }

            if (node?.localName === "fluent-menu-button") {
                return node.closest("fluent-menu");
            }
        }

        return null;
    }

    document.addEventListener("pointerdown", (event) => apply(menuFromEvent(event)), true);
    document.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " " || event.key === "ArrowDown") {
            apply(menuFromEvent(event));
        }
    }, true);
    document.addEventListener("beforetoggle", (event) => {
        const list = event.target;
        if (list?.localName !== "fluent-menu-list") {
            return;
        }

        if (event.newState === "open") {
            apply(list.closest("fluent-menu"));
        }
    }, true);
    document.addEventListener("toggle", (event) => {
        const list = event.target;
        if (list?.localName !== "fluent-menu-list") {
            return;
        }

        if (event.newState === "closed") {
            clearPin(list);
            return;
        }

        apply(list.closest("fluent-menu"));
        requestAnimationFrame(() => pinToTrigger(list));
    }, true);
})();

window.netLabKeyboard = {
    observers: new Map(),
    selector: ".native-edit-context, textarea.inputarea",
    setDisabled: function (editorId, disabled) {
        window.netLabKeyboard.dispose(editorId);

        const root = document.getElementById(editorId);
        if (!root) {
            return;
        }

        if (!disabled) {
            root.querySelectorAll(window.netLabKeyboard.selector).forEach((el) => {
                el.removeAttribute("inputmode");
            });
            return;
        }

        const observers = [];
        window.netLabKeyboard.observers.set(editorId, observers);

        let current = null;
        let currentAttrObserver = null;

        const ensureInputMode = (el) => {
            if (el.getAttribute("inputmode") !== "none") {
                el.setAttribute("inputmode", "none");
            }
        };

        const attach = () => {
            const el = root.querySelector(window.netLabKeyboard.selector);
            if (!el || el === current) {
                return;
            }

            current = el;
            if (currentAttrObserver) {
                currentAttrObserver.disconnect();
                observers.splice(observers.indexOf(currentAttrObserver), 1);
            }

            ensureInputMode(el);
            currentAttrObserver = new MutationObserver(() => ensureInputMode(el));
            currentAttrObserver.observe(el, { attributes: true, attributeFilter: ["inputmode"] });
            observers.push(currentAttrObserver);
        };

        const rootObserver = new MutationObserver(attach);
        rootObserver.observe(root, { childList: true, subtree: true });
        observers.push(rootObserver);
        attach();
    },
    dispose: function (editorId) {
        const observers = window.netLabKeyboard.observers.get(editorId);
        observers?.forEach((observer) => observer.disconnect());
        window.netLabKeyboard.observers.delete(editorId);
    }
};

window.netLabVim = {
    adapters: new Map(),
    enable: function (editorId, statusBarId) {
        window.netLabVim.dispose(editorId);
        if (typeof window.jslib?.EnableVimMode !== "function") {
            return;
        }

        const holder = window.blazorMonaco?.editors?.find((entry) => entry.id === editorId);
        if (!holder?.editor) {
            return;
        }

        const adapter = window.jslib.EnableVimMode(editorId, statusBarId);
        if (adapter) {
            window.netLabVim.adapters.set(editorId, adapter);
        }
    },
    dispose: function (editorId) {
        const adapter = window.netLabVim.adapters.get(editorId);
        if (!adapter) {
            return;
        }

        try {
            adapter.dispose();
        } catch (e) {
        }

        window.netLabVim.adapters.delete(editorId);
    }
};
