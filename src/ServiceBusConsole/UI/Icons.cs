namespace ServiceBusConsole.UI;

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
