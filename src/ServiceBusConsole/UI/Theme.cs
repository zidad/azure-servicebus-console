namespace ServiceBusConsole.UI;

/// <summary>Functional colors: pick by meaning, not by look.</summary>
public static class Theme
{
    public static readonly Color Accent = Color.DodgerBlue1;
    public static readonly Color Muted = Color.Grey50;
    public static readonly Color Border = Color.Grey37;
    public static readonly Color Danger = Color.IndianRed1;
    public static readonly Color Warning = Color.Orange1;
    public static readonly Color Success = Color.Green3_1;
    public static readonly Color Scheduled = Color.SkyBlue2;
    public static readonly Color Transfer = Color.MediumPurple2;

    /// <summary>Background of a count that just changed; fades to <see cref="ChangedFadeTo"/>, a dark terminal background.</summary>
    public static readonly Color Changed = new(140, 110, 0);
    public static readonly Color ChangedFadeTo = new(30, 30, 30);

    public static Color NamespaceStatus(string? status) => status switch
    {
        "Active" => Success,
        null or "" => Muted,
        _ => Warning,
    };
}
