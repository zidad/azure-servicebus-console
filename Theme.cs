using Spectre.Console;

namespace ServiceBusConsole;

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

/// <summary>
/// Emoji with default emoji presentation only: they are two cells wide everywhere.
/// Symbols that need U+FE0F (⚠️, 🗑️, ⬅️) render one cell wide in some terminals and break table alignment.
/// </summary>
public static class Icons
{
    public const string Connection = "🔌";
    public const string AzureSubscription = "🔑";
    public const string Emulator = "🧪";
    public const string Namespace = "🌐";
    public const string Queue = "📬";
    public const string Topic = "📢";
    public const string Subscription = "📥";
    public const string Message = "📨";
    public const string DeadLetter = "💀";
    public const string Scheduled = "⏰";
    public const string Transfer = "🔁";
    public const string Peek = "👀";
    public const string Locked = "🔒";
    public const string Unlocked = "🔓";
    public const string Received = "❗";
    public const string Filter = "🔍";
    public const string Refresh = "🔄";
    public const string Delete = "❌";
    public const string Purge = "🧹";
    public const string Back = "🔙";
    public const string Send = "📤";
    public const string Retry = "🔁";
    public const string Complete = "✅";
    public const string Active = "✅";
    public const string Inactive = "⛔";
    public const string Properties = "📋";
    public const string Body = "📄";
    public const string Confirm = "❓";
    public const string Error = "❌";
    public const string Selected = "✅";
    public const string Done = "✅";
    public const string Running = "⏳";
    public const string Pending = "⬜";
    public const string Cancelled = "🛑";
    public const string Unselected = "⬜";

    public static string NamespaceStatus(string? status) => status switch
    {
        "Active" => Active,
        null or "" => "",
        _ => Inactive,
    };
}
