using Microsoft.AspNetCore.Components.Web;

namespace ServiceBusConsole.Components.Lists;

/// <summary>
/// Cascaded by <see cref="SelectableList{TItem}"/>: the selected row within the visible page, and the
/// handlers a <see cref="NavigableTBody"/> delegates navigation keys and row clicks to.
/// </summary>
public sealed record ListSelection(
    int RelativeSelectedIndex,
    Func<KeyboardEventArgs, Task> HandleKeyDown,
    Func<int, Task> HandleRowClick);

/// <summary>The visible page of a <see cref="SelectableList{TItem}"/>.</summary>
public sealed record ListPage<TItem>(IReadOnlyList<TItem> Items, int CurrentOffset);
