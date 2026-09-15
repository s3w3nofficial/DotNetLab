using DotNetLab;
using DotNetLab.Lab;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Runtime.Versioning;

var builder = AppBuilder.CreateDotnetLabWebAssemblyHostBuilder(args);
builder.Services.AddScoped<IUpdateChecker, WebAssemblyUpdateChecker>();

await builder.Build().RunAsync();

[SupportedOSPlatform("browser")]
partial class Program;

/*
[SupportedOSPlatform("browser")]
partial class Program;

file sealed class WebAssemblyAppHostEnvironment(IWebAssemblyHostEnvironment webAssemblyHostEnvironment) : IAppHostEnvironment
{
    public string Environment => webAssemblyHostEnvironment.Environment;
    public string BaseAddress => webAssemblyHostEnvironment.BaseAddress;

    public string? LabUrlPrefix => null;

    public DesktopAppLink DesktopAppLink { get; } = new()
    {
        Url = App.NativeAppsLink,
        Title = "Native apps available",
        Description = "Faster version of .NET Lab running on full .NET.",
    };

    public bool SupportsWebWorkers => true;
    public bool SupportsThreads => false;

    public ValueTask<bool> HasHardwareKeyboardAsync() => new(true);
}

file sealed class WebAssemblyWorkerConfigurer : IWorkerConfigurer
{
    public void ConfigureWorkerServices(ServiceCollection services)
    {
    }
}

file sealed class WebAssemblyCompilerOutputPlugin : ICompilerOutputPlugin
{
    public string GetText(
        OutputInfo? outputInfo,
        CompiledFileLazyResult result,
        out OutputDisclaimer outputDisclaimer,
        ref string? language)
    {
        if (result.Metadata?.MessageKind == MessageKind.JitAsmUnavailable)
        {
            if (outputInfo?.CachedOutput is { Text: { } cachedText, Language: var cachedLanguage } &&
                cachedText != result.Text)
            {
                outputDisclaimer = OutputDisclaimer.JitAsmUnavailableUsingCached;
                language = cachedLanguage;
                return cachedText;
            }

            outputDisclaimer = OutputDisclaimer.None;
            language = null;
            return $"""
                JIT disassembler is not available on this platform.
                Please use a native app instead ({App.NativeAppsLink}).

                """;
        }

        outputDisclaimer = OutputDisclaimer.None;
        return result.Text;
    }
}
*/
