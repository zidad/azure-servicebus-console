using Microsoft.AspNetCore.Components.Web;

namespace ServiceBusConsole.UI;

/// <summary>
/// A page's key: what its footer (<see cref="KeyHints"/>) and help (<see cref="HelpDialog"/>) show, and what
/// <see cref="Dispatch"/> runs. A page lists its bindings once, so the three can't drift apart.
/// </summary>
/// <param name="Key">The <see cref="KeyboardEventArgs.Key"/> it answers to: "p", "D" (Shift), "/", "Enter".</param>
/// <param name="Label">What it does, with its icon: "❌ Delete".</param>
public sealed record KeyBinding(string Key, string Label, Action Run)
{
    /// <summary>Other keys for it, such as the F-key of earlier versions.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>With Ctrl: ^r.</summary>
    public bool Ctrl { get; init; }

    /// <summary>Deletes or moves messages: a red chip.</summary>
    public bool Danger { get; init; }

    /// <summary>False for keys only the help lists, like Space, which the list handles itself.</summary>
    public bool InFooter { get; init; } = true;

    public string Display => Ctrl ? $"^{Key}" : Key switch
    {
        "Enter" => "⏎",
        "Escape" => UIStrings.KeyBinding_Escape,
        " " => UIStrings.KeyBinding_Space,
        _ => Key,
    };

    public bool Matches(KeyboardEventArgs e) => !e.AltKey && !e.MetaKey && (Ctrl
        ? e.CtrlKey && string.Equals(e.Key, Key, StringComparison.OrdinalIgnoreCase) || !e.CtrlKey && Aliases.Contains(e.Key)
        : !e.CtrlKey && (e.Key == Key || Aliases.Contains(e.Key)));

    /// <summary>Runs the first binding <paramref name="e"/> matches; false when none does.</summary>
    public static bool Dispatch(IEnumerable<KeyBinding> bindings, KeyboardEventArgs e)
    {
        if (bindings.FirstOrDefault(b => b.Matches(e)) is not { } binding) return false;
        binding.Run();
        return true;
    }
}
