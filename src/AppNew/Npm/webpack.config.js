const path = require("path");

module.exports = {
    // BlazorMonaco provides the global Monaco instance. Bundling monaco-vim's peer
    // dependency creates a second editor runtime with separate provider registries.
    externals: {
        "monaco-editor": "monaco",
        "monaco-editor/esm/vs/editor/editor.api": "monaco",
    },
    module: {
        rules: [
            {
                test: /\.(js|jsx)$/,
                exclude: /node_modules/,
                use: {
                    loader: "babel-loader"
                },
            },
            {
                test: /\.css$/i,
                use: ["style-loader", "css-loader"],
            },
        ]
    },
    output: {
        path: path.resolve(__dirname, '../wwwroot/js'),
        filename: "jslib.js",
        library: "jslib"
    }
};