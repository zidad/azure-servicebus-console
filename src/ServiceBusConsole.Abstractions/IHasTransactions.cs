namespace ServiceBusConsole.Abstractions;

/// <summary>A namespace with transactions; the Basic tier and the local emulator have none.</summary>
public interface IHasTransactions
{
    ITransactionService Transactions { get; }
}
