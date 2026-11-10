namespace ServiceBusConsole.UI;

/// <summary>A row of a dialog's label grid (From / To / How / Note); <see cref="Value"/> may span lines.</summary>
public readonly record struct DialogField(string Label, string Value, bool Warning = false);
