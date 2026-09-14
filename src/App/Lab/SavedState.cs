using System.Diagnostics.CodeAnalysis;

namespace DotNetLab.Lab;

partial class Page
{
    private SavedState savedState = SavedState.Initial;
    private string? currentSlug;

    /// <summary>
    /// Only if this is true, edits to user preferences inside <see cref="savedState"/> will be preserved.
    /// This is true in initial clean state (when user preferences are loaded from local storage).
    /// This is false when loading a state from URL (so the shared snippet's settings don't override user preferences).
    /// </summary>
    private bool editingUserPreferences;

    [MemberNotNull(nameof(currentSlug))]
    private void RefreshCurrentSlug()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        currentSlug = uri.Fragment.TrimStart('#');
    }

    private async Task LoadStateFromUrlAsync()
    {
        if (currentSlug is null)
        {
            RefreshCurrentSlug();
        }

        var slug = currentSlug;

        var (state, loadPreferences) = slug switch
        {
            _ when string.IsNullOrWhiteSpace(slug) => (SavedState.Initial, true),
            _ when WellKnownSlugs.ShorthandToState.TryGetValue(slug, out var wellKnownState) => (wellKnownState, false),
            _ => (Compressor.Uncompress(slug), false),
        };

        // Sanitize the state to avoid crashes if possible
        // (anything can be in the user-provided URL hash,
        // but our app expects some invariants, like non-default ImmutableArrays).
        if (state.Inputs.IsDefault)
        {
            state = state with { Inputs = [] };
        }

        if (loadPreferences)
        {
            state = state.WithPreferences(SettingsService.CompilationPreferences);
        }

        // Load new inputs (don't change the actual Page fields yet while we do this async work
        // to avoid interference with SaveStateToUrlAsync).
        var newInputs = new List<Input>();
        var activeIndex = state.SelectedInputIndex;
        Input? firstInput = null;
        Input? activeInput = null;
        foreach (var (index, input) in state.Inputs.Index())
        {
            var model = await CreateModelAsync(input);
            Input inputModel = new(input.FileName, model) { NewContent = input.Text };
            newInputs.Add(inputModel);

            if (index == 0)
            {
                firstInput = inputModel;
            }

            if (index == activeIndex)
            {
                activeInput = inputModel;
            }
        }

        Input? newConfiguration;
        if (state.Configuration is { } savedConfiguration)
        {
            var input = InitialCode.Configuration.ToInputCode() with { Text = savedConfiguration };
            newConfiguration = new(input.FileName, await CreateModelAsync(input)) { NewContent = input.Text };
        }
        else
        {
            newConfiguration = null;
        }

        var selectInput = activeInput ?? firstInput;

        // Now change page fields without awaits.
        Input[] oldInputs;
        using (Util.EnsureSync())
        {
            oldInputs = inputs.ToArray();
            inputs.Clear();
            inputs.AddRange(newInputs);
            configuration = newConfiguration;
            savedState = state;
            editingUserPreferences = loadPreferences;
            activeInputTabId = IndexToInputTabId(activeIndex);
            currentInput = selectInput;
            DisplayOutputType = savedState.SelectedOutputType;
        }

        // Dispose old inputs.
        foreach (var input in oldInputs)
        {
            await input.DisposeAsync();
        }

        await OnWorkspaceChangedAsync();

        if (selectInput != null)
        {
            await inputEditor.SetModel(selectInput.Model);
        }

        // Try loading from cache.
        if (!await TryLoadFromTemplateCacheAsync(state, updateOutput: false) &&
            SettingsService.EnableCaching)
        {
            _ = TryLoadFromCacheAsync(state, updateOutput: true);
        }
        else
        {
            await UpdateOutputDisplayAsync(
                updateTimestampMode: OutputActionMode.Never,
                storeInCacheMode: OutputActionMode.Never);
        }

        // Load settings.
        await settings.LoadFromStateAsync(savedState);
    }

    private bool NavigateToSlug(string slug)
    {
        if (slug != currentSlug)
        {
            NavigationManager.NavigateTo(NavigationManager.BaseUri + "#" + slug, forceLoad: false);
            currentSlug = slug;
            return true;
        }

        return false;
    }

    internal async Task<SavedState> SaveStateToUrlAsync(Func<SavedState, SavedState>? updater = null, bool savePreferences = false)
    {
        // Always save the current editor texts.
        var inputsToSave = await getInputsAsync();
        var configurationToSave = configuration is null ? null : await configuration.Model.GetTextAsync();

        using (Util.EnsureSync())
        {
            savedState = savedState with
            {
                Inputs = inputsToSave,
                Configuration = configurationToSave,
            };

            if (updater != null)
            {
                savedState = updater(savedState);
            }
        }

        if (savePreferences && editingUserPreferences)
        {
            var preferences = savedState.GetPreferences();
            SettingsService.CompilationPreferences = preferences;
            await SettingsService.SaveAsync();
        }

        var newSlug = Compressor.Compress(savedState);

        if (WellKnownSlugs.FullSlugToShorthand.TryGetValue(newSlug, out var wellKnownSlug))
        {
            newSlug = wellKnownSlug;
        }

        NavigateToSlug(newSlug);

        return savedState;

        async Task<ImmutableArray<InputCode>> getInputsAsync()
        {
            using var _ = await inputsLock.LockAsync();
            var builder = ImmutableArray.CreateBuilder<InputCode>(inputs.Count);
            foreach (var (fileName, model) in inputs)
            {
                var text = await model.GetTextAsync();
                builder.Add(new() { FileName = fileName, Text = text });
            }
            return builder.ToImmutable();
        }
    }
}
