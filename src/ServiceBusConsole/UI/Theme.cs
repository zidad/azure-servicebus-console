using RazorConsole.Core.Rendering;

namespace ServiceBusConsole.UI;

/// <summary>Functional colors: pick by meaning, not by look. They come from the <see cref="Palette"/> in use, chosen at startup.</summary>
public static class Theme
{
    public static Palette Palette { get; private set; } = Palette.Dark;

    /// <summary>Before the UI starts.</summary>
    public static void Use(Palette palette) => Palette = palette;

    public static Color Accent => Palette.Accent;
    public static Color Muted => Palette.Muted;
    public static Color Border => Palette.Border;
    public static Color Danger => Palette.Danger;
    public static Color Warning => Palette.Warning;
    public static Color Success => Palette.Success;
    public static Color Scheduled => Palette.Scheduled;
    public static Color Transfer => Palette.Transfer;

    /// <inheritdoc cref="Palette.Selection"/>
    public static Color Selection => Palette.Selection;
    public static Color OnSelection => Palette.OnSelection;

    /// <inheritdoc cref="Palette.Hover"/>
    public static Color Hover => Palette.Hover;
    public static Color OnHover => Palette.OnHover;

    /// <summary>Metrics chart series; dead-lettered messages use <see cref="Danger"/>.</summary>
    public static Color Incoming => Accent;
    public static Color Outgoing => Success;
    public static Color Backlog => Warning;

    /// <inheritdoc cref="Palette.Changed"/>
    public static Color Changed => Palette.Changed;
    public static Color ChangedFadeTo => Palette.ChangedFadeTo;

    /// <summary>A list's scrollbar: the thumb in <see cref="Accent"/> while it has focus.</summary>
    public static ScrollbarSettings Scrollbar() => new(trackColor: Border, thumbColor: Muted, trackFocusedColor: Muted, thumbFocusedColor: Accent);

    public static Color NamespaceStatus(string? status) => status switch
    {
        "Active" => Success,
        null or "" => Muted,
        _ => Warning,
    };
}
