using System.Collections.Frozen;
using System.Net;
using System.Runtime.InteropServices;

namespace DotNetLab.Lab;

internal sealed class AssemblyDownloader
{
    private readonly HttpClient client;
    private readonly Func<DotNetBootConfig?> bootConfigProvider;
    private readonly Lazy<FrozenDictionary<string, string>> fingerprintedFileNames;

    public AssemblyDownloader(HttpClient client, Func<DotNetBootConfig?> bootConfigProvider)
    {
        this.client = client;
        this.bootConfigProvider = bootConfigProvider;
        fingerprintedFileNames = new(GetFingerprintedFileNames);
    }

    private FrozenDictionary<string, string> GetFingerprintedFileNames()
    {
        var config = bootConfigProvider();

        if (config == null)
        {
            return FrozenDictionary<string, string>.Empty;
        }

        // VirtualPath is "Foo.wasm" (WebCIL) or "Foo.dll" (WasmEnableWebcil=false).
        // Name is the fingerprinted file actually served from _framework/.
        return config.Resources.Assembly
            .Where(static a => !string.IsNullOrEmpty(a.VirtualPath) && !string.IsNullOrEmpty(a.Name))
            .ToFrozenDictionary(
                static a => Path.GetFileNameWithoutExtension(a.VirtualPath),
                static a => a.Name,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<DownloadedAssembly> DownloadAsync(string assemblyFileNameWithoutExtension)
    {
        var fingerprintedFileNames = this.fingerprintedFileNames.Value;

        if (fingerprintedFileNames.TryGetValue(assemblyFileNameWithoutExtension, out var fingerprintedFileName))
        {
            return await DownloadFileAsync(fingerprintedFileName);
        }

        try
        {
            return await DownloadFileAsync($"{assemblyFileNameWithoutExtension}.wasm");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return await DownloadFileAsync($"{assemblyFileNameWithoutExtension}.dll");
        }
    }

    private async Task<DownloadedAssembly> DownloadFileAsync(string fileName)
    {
        var bytes = await client.GetByteArrayAsync($"_framework/{fileName}");
        return new DownloadedAssembly(
            ImmutableCollectionsMarshal.AsImmutableArray(bytes),
            FormatFromFileName(fileName));
    }

    private static AssemblyDataFormat FormatFromFileName(string fileName)
        => fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? AssemblyDataFormat.Dll
            : AssemblyDataFormat.Webcil;
}

internal readonly record struct DownloadedAssembly(ImmutableArray<byte> Data, AssemblyDataFormat Format);

public sealed class DotNetBootConfig
{
    public required DotNetBootConfigResources Resources { get; init; }
}

public sealed class DotNetBootConfigResources
{
    public required ImmutableArray<DotNetBootConfigAssembly> Assembly { get; init; }
}

public sealed class DotNetBootConfigAssembly
{
    public required string Name { get; init; }
    public required string VirtualPath { get; init; }
}
