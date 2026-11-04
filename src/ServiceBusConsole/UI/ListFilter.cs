using Microsoft.AspNetCore.Components.Web;

namespace ServiceBusConsole.UI;

/// <summary>
/// A list's name filter. <c>/</c> starts editing, so letters are free for commands the rest of the time; while
/// editing, typing and Backspace change it, Enter keeps it and Escape clears it. Escape also clears a kept filter.
/// <see cref="TypeToFilter"/> skips the <c>/</c> for pickers, where letters are only ever a filter.
/// </summary>
public sealed class ListFilter
{
    public string? Text { get; private set; }

    public bool Editing { get; private set; }

    public bool TypeToFilter { get; set; }

    public bool IsEmpty => string.IsNullOrEmpty(Text);

    /// <summary>Starts editing, as <c>/</c> does.</summary>
    public void Edit() => Editing = true;

    public void Clear()
    {
        Text = null;
        Editing = false;
    }

    /// <summary>Whether <paramref name="e"/> was a filter key, and whether it changed <see cref="Text"/>.</summary>
    public FilterKey Handle(KeyboardEventArgs e)
    {
        if (e.CtrlKey || e.AltKey || e.MetaKey) return FilterKey.Ignored;
        if (Editing || TypeToFilter)
        {
            switch (e.Key)
            {
                case "Escape" when !IsEmpty || Editing:
                    var had = !IsEmpty;
                    Clear();
                    return had ? FilterKey.Changed : FilterKey.Handled;
                case "Enter" when Editing:
                    Editing = false;
                    return FilterKey.Handled;
            }
            if (FilterEdit.From(Text, e) is { } edit)
            {
                Text = edit.Filter;
                return FilterKey.Changed;
            }
            return FilterKey.Ignored;
        }
        switch (e.Key)
        {
            case "/":
                Editing = true;
                return FilterKey.Handled;
            case "Escape" when !IsEmpty:
                Clear();
                return FilterKey.Changed;
            default:
                return FilterKey.Ignored;
        }
    }
}
