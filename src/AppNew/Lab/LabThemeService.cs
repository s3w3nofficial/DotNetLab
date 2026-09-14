using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetLab.Lab;

public sealed class LabThemeService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly IThemeService _fluent;
    private readonly LabWorkspaceState _state;
    private DotNetObjectReference<LabThemeService>? _self;
    private bool _listening;

    public LabThemeService(IJSRuntime js, IThemeService fluent, LabWorkspaceState state)
    {
        _js = js;
        _fluent = fluent;
        _state = state;
    }

    public async Task InitializeAsync()
    {
        var preference = LabTheme.NormalizePreference(
            await InvokeAsync("netLabTheme.readPreference", "dark"));
        var dark = await ResolveDarkAsync(preference);
        await ApplyAsync(preference, dark, persist: false);

        if (_listening)
        {
            return;
        }

        _self = DotNetObjectReference.Create(this);
        try
        {
            await _js.InvokeVoidAsync("netLabTheme.listenSystem", _self);
            _listening = true;
        }
        catch (JSException)
        {
        }
    }

    public async Task SetPreferenceAsync(string preference)
    {
        preference = LabTheme.NormalizePreference(preference);
        var dark = await ResolveDarkAsync(preference);
        await ApplyAsync(preference, dark, persist: true);
    }

    [JSInvokable]
    public async Task OnSystemThemeChanged(bool dark)
    {
        if (!string.Equals(_state.AppTheme, "system", StringComparison.Ordinal))
        {
            return;
        }

        await ApplyAsync("system", dark, persist: false);
    }

    public async ValueTask DisposeAsync()
    {
        _self?.Dispose();
        _self = null;
        try
        {
            await _js.InvokeVoidAsync("netLabTheme.stopListening");
        }
        catch (JSException)
        {
        }
    }

    private async Task ApplyAsync(string preference, bool dark, bool persist)
    {
        try
        {
            await _js.InvokeVoidAsync("netLabTheme.applyDocument", dark);
            if (persist)
            {
                await _js.InvokeVoidAsync("netLabTheme.persist", preference);
            }

            await _fluent.SetThemeAsync(LabTheme.CreateSettings(dark ? ThemeMode.Dark : ThemeMode.Light));
        }
        catch (JSException)
        {
        }

        _state.SetTheme(preference, dark);

        try
        {
            await _js.InvokeVoidAsync("netLabMonaco.applyTheme", dark);
        }
        catch (JSException)
        {
        }
    }

    private async Task<bool> ResolveDarkAsync(string preference)
    {
        try
        {
            return await _js.InvokeAsync<bool>("netLabTheme.resolveDark", preference);
        }
        catch (JSException)
        {
            return preference != "light";
        }
    }

    private async Task<string> InvokeAsync(string identifier, string fallback)
    {
        try
        {
            return await _js.InvokeAsync<string>(identifier);
        }
        catch (JSException)
        {
            return fallback;
        }
    }
}
