namespace ServiceBusConsole.Abstractions;

/// <summary>A broker whose messages are numbered, so a peek can start at a number (Service Bus: the sequence number).</summary>
public interface IHasSeek
{
    ISeekService Seek { get; }
}
