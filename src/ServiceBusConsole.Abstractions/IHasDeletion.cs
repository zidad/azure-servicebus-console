namespace ServiceBusConsole.Abstractions;

/// <summary>A namespace whose entities can be deleted; the local emulator can't.</summary>
public interface IHasDeletion
{
    IDeletionService Deletion { get; }
}
