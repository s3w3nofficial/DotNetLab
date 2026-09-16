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

(function watchMonacoMenus() {
    const apply = () => window.netLabMonaco?.styleMenus?.();
    const observer = new MutationObserver(apply);
    observer.observe(document.documentElement, { childList: true, subtree: true });
    document.addEventListener("contextmenu", () => setTimeout(apply, 0), true);
})();
