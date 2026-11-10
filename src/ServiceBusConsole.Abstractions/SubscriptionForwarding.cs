namespace ServiceBusConsole.Abstractions;

/// <summary>A subscription's auto-forward targets; null when it does not forward.</summary>
public sealed record SubscriptionForwarding(string TopicName, string Name, string? ForwardTo, string? ForwardDeadLetteredMessagesTo);
