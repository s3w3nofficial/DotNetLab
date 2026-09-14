export function enableVimMode(editorId, statusBarId) {
    // Monaco is loaded asynchronously, so resolve monaco-vim only when VIM mode is enabled.
    const { initVimMode } = require('monaco-vim');
    const editor = window.blazorMonaco.editors.find((e) => e.id === editorId).editor;
    const statusBar = document.getElementById(statusBarId);

    return initVimMode(editor, statusBar);
}
