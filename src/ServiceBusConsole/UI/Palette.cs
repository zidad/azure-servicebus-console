namespace ServiceBusConsole.UI;

/// <summary>
/// The colors of a <see cref="Theme"/>. Built from the terminal's 16 palette colors, so the terminal's own color
/// scheme decides their shade; only the changed-count fade needs RGB, to blend.
/// </summary>
public sealed record Palette
{
    public required Color Accent { get; init; }
    public required Color Muted { get; init; }
    public required Color Border { get; init; }
    public required Color Danger { get; init; }
    public required Color Warning { get; init; }
    public required Color Success { get; init; }
    public required Color Scheduled { get; init; }
    public required Color Transfer { get; init; }

    /// <summary>Text of a row that is done with: a settled message in a locked list.</summary>
    public required Color Settled { get; init; }

    /// <summary>Background of the selected row, and text on it.</summary>
    public required Color Selection { get; init; }
    public required Color OnSelection { get; init; }

    /// <summary>Background of a key in the footer (<see cref="KeyHints"/>), and its text.</summary>
    public required Color Chip { get; init; }
    public required Color OnChip { get; init; }

    /// <summary>Text on a <see cref="Danger"/> background: the chip of a key that deletes or moves messages.</summary>
    public required Color OnDanger { get; init; }

    /// <summary>Text on an <see cref="Accent"/> or <see cref="Warning"/> background: the browse mode's badge and strip.</summary>
    public required Color OnAccent { get; init; }
    public required Color OnWarning { get; init; }

    /// <summary>Background of a hovered button, and its text.</summary>
    public required Color Hover { get; init; }
    public required Color OnHover { get; init; }

    /// <summary>JSON highlighting: property names, strings, numbers, and true/false/null.</summary>
    public required Color JsonKey { get; init; }
    public required Color JsonString { get; init; }
    public required Color JsonNumber { get; init; }
    public required Color JsonConst { get; init; }

    /// <summary>Background of a count that just changed, fading to <see cref="ChangedFadeTo"/>, about the terminal's background.</summary>
    public required Color Changed { get; init; }
    public required Color ChangedFadeTo { get; init; }

    /// <summary>For a dark background: the bright variants.</summary>
    public static readonly Palette Dark = new()
    {
        Accent = Color.Blue,
        Muted = Color.Grey,
        Border = Color.Grey,
        Danger = Color.Red,
        Warning = Color.Yellow,
        Success = Color.Lime,
        Scheduled = Color.Aqua,
        Transfer = Color.Fuchsia,
        Settled = Color.Grey,
        Selection = Color.Navy,
        OnSelection = Color.White,
        Chip = Color.Grey,
        OnChip = Color.White,
        OnDanger = Color.Black,
        OnAccent = Color.Black,
        OnWarning = Color.Black,
        Hover = Color.Silver,
        OnHover = Color.Black,
        JsonKey = Color.Blue,
        JsonString = Color.Lime,
        JsonNumber = Color.Aqua,
        JsonConst = Color.Fuchsia,
        Changed = new(140, 110, 0),
        ChangedFadeTo = new(30, 30, 30),
    };

    /// <summary>For a light background: the normal variants, which light schemes keep dark enough to read.</summary>
    public static readonly Palette Light = new()
    {
        Accent = Color.Navy,
        Muted = Color.Grey,
        Border = Color.Grey,
        Danger = Color.Maroon,
        Warning = Color.Olive,
        Success = Color.Green,
        Scheduled = Color.Teal,
        Transfer = Color.Purple,
        Settled = Color.Silver,
        Selection = Color.Navy,
        OnSelection = Color.White,
        Chip = Color.Silver,
        OnChip = Color.Black,
        OnDanger = Color.White,
        OnAccent = Color.White,
        OnWarning = Color.White,
        Hover = Color.Silver,
        OnHover = Color.Black,
        JsonKey = Color.Navy,
        JsonString = Color.Green,
        JsonNumber = Color.Teal,
        JsonConst = Color.Purple,
        Changed = new(255, 215, 95),
        ChangedFadeTo = new(250, 250, 250),
    };

    /// <summary>
    /// <paramref name="theme"/> (auto, dark or light); auto reads the background from <c>COLORFGBG</c> ("fg;bg", set by
    /// some terminals), dark when unknown. Null for an unknown theme name.
    /// </summary>
    public static Palette? For(string? theme, string? colorFgBg) => theme?.ToLowerInvariant() switch
    {
        null or "auto" => IsLightBackground(colorFgBg) ? Light : Dark,
        "dark" => Dark,
        "light" => Light,
        _ => null,
    };

    // The last field is the background's palette index: 7 (white) and 15 (bright white) are light.
    private static bool IsLightBackground(string? colorFgBg) =>
        colorFgBg?.Split(';').LastOrDefault() is { } bg && int.TryParse(bg, out var index) && index is 7 or 15;
}
