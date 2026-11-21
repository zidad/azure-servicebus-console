namespace ServiceBusConsole.CommandLine;

/// <summary>What <c>list</c> shows; <see cref="Entities"/> is queues and subscriptions, the ones holding messages.</summary>
public enum ListKind { Entities, Queues, Topics, Subscriptions }
