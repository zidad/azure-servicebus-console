namespace ServiceBusConsole.Abstractions;

/// <summary>Something the user did or saw in this session; <see cref="Source"/> is the entity it concerned, if any.</summary>
public sealed record ActivityEntry(DateTimeOffset At, ActivityKind Kind, string Text, MessageSource? Source = null);
