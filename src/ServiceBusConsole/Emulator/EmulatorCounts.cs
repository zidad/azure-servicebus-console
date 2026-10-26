using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.Emulator;

/// <summary>The emulator has no management API: counts come from peeking (up to <see cref="MaxPeek"/>).</summary>
public static class EmulatorCounts
{
    private const int MaxPeek = 1000;

    public static async Task<(long Active, long DeadLetter)> PeekAsync(
        ServiceBusClient client, string entity, string? subscription, ILogger logger, CancellationToken ct)
    {
        async Task<long> CountAsync(SubQueue subQueue)
        {
            var options = new ServiceBusReceiverOptions { SubQueue = subQueue };
            try
            {
                await using var receiver = subscription is null
                    ? client.CreateReceiver(entity, options)
                    : client.CreateReceiver(entity, subscription, options);
                return (await receiver.PeekMessagesAsync(MaxPeek, cancellationToken: ct)).Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug("Peek {SubQueue} of {Entity}/{Subscription}: {Error}", subQueue, entity, subscription, ex.Message);
                return 0;
            }
        }

        return (await CountAsync(SubQueue.None), await CountAsync(SubQueue.DeadLetter));
    }

    public static EmulatorNamespace Config() =>
        EmulatorConfigReader.TryLoad()
            ?? throw new InvalidOperationException("emulator/Config.json not found. Run from the project directory.");
}
