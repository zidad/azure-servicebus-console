namespace ServiceBusConsole.CommandLine;

/// <summary>What <c>azsb list</c> shows; <see cref="Entities"/> is queues and subscriptions, the ones holding messages.</summary>
public enum ListKind { Entities, Queues, Topics, Subscriptions }
