namespace ServiceBusConsole.UI;

/// <summary>
/// Whether pages show the metrics panel: on tall terminals until F12 chooses, then that choice, kept across runs.
/// Never on a terminal too short to keep a usable list next to it.
/// </summary>
public sealed class MetricsVisibility(FileCache cache, MetricsOptions options)
{
    private const string Key = "settings/metrics-panel.json";

    /// <summary>A list page's header, table with 5 rows, the panel and the buttons.</summary>
    public const int MinHeight = FullscreenLayout.Header + FullscreenLayout.RoundedTableChrome + 5
        + FullscreenLayout.MetricsPanel + FullscreenLayout.FooterButtons + 1;

    private bool? _shown;

    public bool IsVisible(int terminalHeight, int minHeight = MinHeight) =>
        terminalHeight >= minHeight && IsChosen(terminalHeight);

    /// <summary>Reads the remembered choice; call once at startup.</summary>
    public async Task LoadAsync()
    {
        if (await cache.LoadAsync<bool>(Key) is [var shown]) _shown = shown;
    }

    /// <summary>Flips the choice at once, also where the panel doesn't fit; the returned task saves it.</summary>
    public Task ToggleAsync(int terminalHeight)
    {
        var shown = !IsChosen(terminalHeight);
        _shown = shown;
        return cache.SaveAsync(Key, [shown]);
    }

    private bool IsChosen(int terminalHeight) => _shown ?? terminalHeight >= options.AutoShowHeight;
}
