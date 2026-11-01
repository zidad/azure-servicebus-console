using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>
/// A dead-lettered message and its queue, or a subscription and its topic, are one top-level entity, so a plain client's
/// transaction covers sending to one and completing on the other. EnableCrossEntityTransactions isn't needed, and would
/// make the namespace's client fail receives on any entity after the first.
/// </summary>
internal sealed class TransactionService(ServiceBusClient bus, ILogger<TransactionService> logger) : ITransactionService
{
    public int MaxMessages => 100;

    public async Task MoveAsync(MessageSource source, ILockedMessages locked, IReadOnlyList<MessageInfo> messages, CancellationToken ct = default)
    {
        if (messages.Count > MaxMessages) throw new ArgumentException($"At most {MaxMessages} messages per transaction.", nameof(messages));
        var held = locked as LockedMessages ?? throw new ArgumentException("These messages were not locked in this namespace.", nameof(locked));
        await using var sender = bus.CreateSender(source.SendTarget);
        // Committing blocks its thread until Service Bus confirms: keep that off the caller's (UI) thread.
        await Task.Run(() => held.CompleteInTransactionAsync(messages, token => MessageService.SendInBatchesAsync(sender, messages, token).LastOrDefaultAsync(token).AsTask(), ct), ct);
        logger.LogInformation("Moved {Count} messages to {Target} in one transaction", messages.Count, source.SendTarget);
    }
}
