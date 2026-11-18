namespace ServiceBusConsole.Abstractions;

/// <summary>What a peek does to the messages it reads.</summary>
public enum PeekKind
{
    /// <summary>Reads without touching them.</summary>
    NonDestructive,

    /// <summary>Receives and puts them back: their delivery count goes up, and a delivery limit can dead-letter them.</summary>
    Requeues,
}
