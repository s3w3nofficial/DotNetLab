using DotNetLab.Lab;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

namespace DotNetLab;

public static class AppBuilder
{
    public static WebAssemblyHostBuilder CreateDotnetLabWebAssemblyHostBuilder(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        
        builder.Services.AddFluentUIComponents();
        builder.Services.AddScoped<LabWorkspaceState>();
        builder.Services.AddScoped<LabUrlSync>();
        builder.Services.AddScoped<LabThemeService>();
        builder.Services.AddScoped<LabPlatform>();
        builder.Services.AddScoped<LabShare>();
        builder.Services.AddScoped<BlazorMonacoInterop>();
        builder.Services.AddScoped<LabLanguageServices>();

        // Compiler stack lives in its own container (NuGet/SDK downloads, Roslyn load).
        // Do not register that IServiceProvider into Blazor DI — it would replace the UI host.
        builder.Services.AddSingleton(new WorkerHost(
            WorkerServices.Create(
                baseUrl: builder.HostEnvironment.BaseAddress,
                logLevel: LogLevel.Information)));

        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddScoped(sp => new HttpClient {BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)});
        
        return builder;
    }
}
