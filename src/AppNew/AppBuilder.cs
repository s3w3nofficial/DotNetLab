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
        builder.Services.AddScoped<LabSettings>();
        builder.Services.AddScoped<TemplateCache>();
        builder.Services.AddScoped<InputOutputCache>();
        builder.Services.AddScoped<BlazorMonacoInterop>();
        builder.Services.AddScoped<LabLanguageServices>();
        builder.Services.AddScoped<LabCursorSync>();
        builder.Services.AddScoped<IUpdateChecker, DisabledUpdateChecker>();
        builder.Services.AddSingleton<LabLogging>();
        builder.Services.AddOptions<LoggerFilterOptions>().Configure<LabLogging>((options, logging) =>
        {
            options.AddFilter("DotNetLab.*", logLevel => logLevel >= logging.LogLevel);
        });

        // Compiler stack lives in its own container (NuGet/SDK downloads, Roslyn load).
        // Do not register that IServiceProvider into Blazor DI — it would replace the UI host.
        builder.Services.AddScoped<WorkerHost>();

        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddScoped(sp => new HttpClient {BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)});
        
        return builder;
    }
}
