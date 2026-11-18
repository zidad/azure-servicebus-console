using System.Globalization;

namespace ServiceBusConsole.Abstractions;

/// <summary>Identifies a message within its source, as its transport defines it: selection, holds and settled state are keyed on it.</summary>
public readonly record struct MessageKey(string Value)
{
    public static MessageKey From(long number) => new(number.ToString(CultureInfo.InvariantCulture));

    public override string ToString() => Value;
}
