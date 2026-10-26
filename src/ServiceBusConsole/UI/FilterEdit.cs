using Microsoft.AspNetCore.Components.Web;

namespace ServiceBusConsole.UI;

/// <summary>A type-to-filter edit: Backspace drops the last character, a printable key appends.</summary>
public readonly record struct FilterEdit(string? Filter)
{
    /// <summary>The edited filter, or null when <paramref name="e"/> does not edit it.</summary>
    public static FilterEdit? From(string? filter, KeyboardEventArgs e) => e.Key switch
    {
        "Backspace" => new(string.IsNullOrEmpty(filter) ? null : filter[..^1]),
        { Length: 1 } key when !char.IsControl(key[0]) => new((filter ?? "") + key),
        _ => null,
    };
}
