namespace ServiceBusConsole.Abstractions;

/// <summary>A namespace with topics and subscriptions; the Basic tier has none.</summary>
public interface IHasTopics
{
    ITopicService Topics { get; }
}
