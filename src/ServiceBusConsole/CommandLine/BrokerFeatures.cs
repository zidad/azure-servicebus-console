namespace ServiceBusConsole.CommandLine;

/// <summary>The features a command needs, or a usage error naming the broker that lacks them.</summary>
public static class BrokerFeatures
{
    public static IPeekService PeekOf(IBroker ns) =>
        ns as IHasPeek is { Peek: var peek } ? peek : throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.FormatBrokerFeatures_NoPeek(ns.Info.Name));

    public static IDeadLetterService DeadLettersOf(IBroker ns) =>
        ns as IHasDeadLetters is { DeadLetters: var deadLetters } ? deadLetters
        : throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.FormatBrokerFeatures_NoDeadLetters(ns.Info.Name));

    public static ISeekService SeekOf(IBroker ns) =>
        ns as IHasSeek is { Seek: var seek } ? seek : throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.FormatBrokerFeatures_NoSeek(ns.Info.Name));
}
