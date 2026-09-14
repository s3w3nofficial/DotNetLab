using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace DotNetLab.Lab;

public sealed class LabUrlSync : IDisposable
{
    private readonly NavigationManager _navigation;
    private readonly LabWorkspaceState _state;
    private readonly IJSRuntime _js;
    private bool _ignoreNextLocation;
    private bool _loaded;

    public LabUrlSync(NavigationManager navigation, LabWorkspaceState state, IJSRuntime js)
    {
        _navigation = navigation;
        _state = state;
        _js = js;
        _state.UrlPersistRequested += SaveAsync;
        _navigation.LocationChanged += OnLocationChanged;
    }

    public async Task LoadFromUriAsync()
    {
        var slug = await ReadBrowserHashAsync();
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = GetSlug(_navigation.Uri);
        }

        _loaded = true;
        await ApplySlugAsync(string.IsNullOrWhiteSpace(slug) ? "csharp" : slug);
    }

    public Task SaveAsync()
    {
        var state = _state.CaptureSavedState();
        var slug = Compressor.Compress(state);
        if (WellKnownSlugs.FullSlugToShorthand.TryGetValue(slug, out var shorthand))
        {
            slug = shorthand;
        }

        if (string.Equals(GetSlug(_navigation.Uri), slug, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        _ignoreNextLocation = true;
        _navigation.NavigateTo(_navigation.BaseUri + "#" + slug,
            new NavigationOptions { ReplaceHistoryEntry = true });
        return Task.CompletedTask;
    }

    public async Task ApplySlugOrUrlAsync(string text)
    {
        var slug = GetSlugFromClipboardText(text);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        await ApplySlugAsync(slug);
        await SaveAsync();
    }

    public static string GetSlugFromClipboardText(string? text)
    {
        text ??= "";
        var hashIndex = text.IndexOf('#');
        return hashIndex >= 0 ? text[(hashIndex + 1)..] : text.Trim();
    }

    private async Task ApplyLocationSlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = await ReadBrowserHashAsync();
        }

        await ApplySlugAsync(string.IsNullOrWhiteSpace(slug) ? "csharp" : slug);
    }

    private async Task ApplySlugAsync(string slug)
    {
        SavedState state;
        if (WellKnownSlugs.ShorthandToState.TryGetValue(slug, out var wellKnown))
        {
            state = wellKnown;
        }
        else
        {
            state = Compressor.Uncompress(slug);
        }

        await _state.ApplySavedStateAsync(state);
    }

    private async Task<string> ReadBrowserHashAsync()
    {
        try
        {
            return await _js.InvokeAsync<string>("netLabUrl.hash") ?? "";
        }
        catch (JSException)
        {
            return "";
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (_ignoreNextLocation)
        {
            _ignoreNextLocation = false;
            return;
        }

        if (!_loaded)
        {
            return;
        }

        var slug = GetSlug(args.Location);
        _ = ApplyLocationSlugAsync(slug);
    }

    private string GetSlug(string uri)
    {
        try
        {
            return (_navigation.ToAbsoluteUri(uri).Fragment ?? "").TrimStart('#');
        }
        catch (UriFormatException)
        {
            return "";
        }
    }

    public void Dispose()
    {
        _state.UrlPersistRequested -= SaveAsync;
        _navigation.LocationChanged -= OnLocationChanged;
    }
}
