namespace ServiceBusConsole.Abstractions;

/// <summary>A broker that can read messages without receiving them for good; see <see cref="IPeekService.Kind"/> for the cost.</summary>
public interface IHasPeek
{
    IPeekService Peek { get; }
}
