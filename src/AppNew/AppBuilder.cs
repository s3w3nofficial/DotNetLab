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
        builder.Services.AddScoped<LabThemeService>();
        builder.Services.AddScoped<LabPlatform>();
        builder.Services.AddScoped<LabShare>();

        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddScoped(sp => new HttpClient {BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)});
        
        return builder;
    }
}
