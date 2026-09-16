using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetLab.Features.Sharing;

public sealed class LabShare
{
    private readonly IJSRuntime _js;
    private readonly NavigationManager _navigation;
    private readonly ILabSharing _state;

    public LabShare(IJSRuntime js, NavigationManager navigation, ILabSharing state)
    {
        _js = js;
        _navigation = navigation;
        _state = state;
    }

    public async Task CopyLinkAsync()
    {
        await _state.PersistUrlAsync(snapshot: true);
        await WriteClipboardAsync(_navigation.Uri);
    }

    public async Task CreateGistAsync()
    {
        await _state.SnapshotEditorsAsync();
        await WriteClipboardAsync(LabLinks.GistSnapshot(_state));
        await OpenExternalAsync(LabLinks.GistNew);
    }

    public Task ReportIssueAsync() => OpenExternalAsync(LabLinks.NewIssue(_state));

    public async Task OpenExternalAsync(string url)
    {
        try
        {
            await _js.InvokeVoidAsync("open", url, "_blank", "noopener,noreferrer");
        }
        catch (JSException)
        {
        }
    }

    private async Task WriteClipboardAsync(string text)
    {
        try
        {
            await _js.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch (JSException)
        {
        }
    }
}
