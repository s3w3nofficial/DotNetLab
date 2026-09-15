namespace DotNetLab.Lab;

/// <summary>
/// Pane layout snapshot (<see cref="LayoutState.Split"/>). Pointer-move stays
/// in JS; persist the final split on pointer-up. Stacked lives in Preferences.
/// Output tabs stay on <see cref="OutputTabLayout"/>.
/// </summary>
public sealed class LayoutStore : StateStore<LayoutState>
{
    public LayoutStore() : base(new LayoutState())
    {
    }
}
