namespace ServiceBusConsole.UI;

/// <summary>
/// Whether pages show the metrics panel: on tall terminals until F12 chooses, then that choice, kept across runs.
/// Never on a terminal too short to keep a usable list next to it.
/// </summary>
public sealed class MetricsVisibility(FileCache cache, MetricsOptions options)
{
    private const string Key = "settings/metrics-panel.json";
    private const int MinHeight = FullscreenLayout.Header + FullscreenLayout.RoundedTableChrome + 5
        + FullscreenLayout.MetricsPanel + FullscreenLayout.FooterButtons + 1;

    private bool? _shown;

    public bool IsVisible(int terminalHeight) =>
        terminalHeight >= MinHeight && (_shown ?? terminalHeight >= options.AutoShowHeight);

    /// <summary>Reads the remembered choice; call once at startup.</summary>
    public async Task LoadAsync()
    {
        if (await cache.LoadAsync<bool>(Key) is [var shown]) _shown = shown;
    }

    /// <summary>Shows or hides the panel at once; the returned task saves the choice.</summary>
    public Task ToggleAsync(int terminalHeight)
    {
        var shown = !IsVisible(terminalHeight);
        _shown = shown;
        return cache.SaveAsync(Key, [shown]);
    }
}
