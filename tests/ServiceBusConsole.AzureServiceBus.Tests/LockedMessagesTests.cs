using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.AzureServiceBus.Tests;

public sealed class LockedMessagesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StartScan_StreamsEachWantedMessage_AndReleasesThePassedOnes()
    {
        var receiver = new FakeReceiver(count: 10, batchSize: 4);

        var (locked, found) = await ScanAsync(receiver, [3, 7], maxScan: 100);

        found.ShouldBe([3L, 7L]);
        locked.IsHeld(3).ShouldBeTrue();
        locked.IsHeld(7).ShouldBeTrue();
        // Batches of 4: 1-4 and 5-8 were received; everything but 3 and 7 went back per batch.
        receiver.Abandoned.Order().ShouldBe([1L, 2L, 4L, 5L, 6L, 8L]);
        receiver.Received.ShouldBe(8);
        await locked.DisposeAsync();
    }

    [Fact]
    public async Task StartScan_WantedMissing_StopsWhenEmptyOrAtMaxScan()
    {
        var empty = new FakeReceiver(count: 5, batchSize: 4);
        var (none, foundNone) = await ScanAsync(empty, [99], maxScan: 100);
        foundNone.ShouldBeEmpty();
        empty.Abandoned.Order().ShouldBe([1L, 2L, 3L, 4L, 5L]);
        await none.DisposeAsync();

        var capped = new FakeReceiver(count: 50, batchSize: 4);
        var (limited, _) = await ScanAsync(capped, [99], maxScan: 10);
        capped.Received.ShouldBe(10);
        await limited.DisposeAsync();
    }

    [Fact]
    public async Task ALostLock_ArrivesOnLockLost_AndTheMessageIsNoLongerHeld()
    {
        var receiver = new FakeReceiver(count: 1, batchSize: 1) { LockFor = TimeSpan.FromSeconds(1), LocksLost = true };
        var locked = LockedMessages.StartReceive(receiver, 1, NullLogger.Instance, Ct);
        var message = await locked.Held.ReadAsync(Ct);

        var lost = await locked.LockLost.ReadAsync(Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), Ct);

        lost.ShouldBe(message.SequenceNumber);
        locked.IsHeld(message.SequenceNumber).ShouldBeFalse();
        await locked.DisposeAsync();
        locked.LockLost.Completion.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task SettlingWhenTheLockIsGone_ThrowsMessageLockLost()
    {
        var receiver = new FakeReceiver(count: 1, batchSize: 1) { LocksLost = true };
        var locked = LockedMessages.StartReceive(receiver, 1, NullLogger.Instance, Ct);
        var message = await locked.Held.ReadAsync(Ct);

        await Should.ThrowAsync<MessageLockLostException>(() => locked.CompleteAsync(message, Ct));

        locked.IsHeld(message.SequenceNumber).ShouldBeFalse();
        await locked.DisposeAsync();
    }

    private static async Task<(LockedMessages Locked, List<long> Found)> ScanAsync(FakeReceiver receiver, long[] wanted, int maxScan)
    {
        var locked = LockedMessages.StartScan(receiver, wanted, maxScan, NullLogger.Instance, Ct);
        var found = await locked.Held.ReadAllAsync(Ct).Select(m => m.SequenceNumber).ToListAsync(Ct);
        return (locked, found);
    }

    private sealed class FakeReceiver(int count, int batchSize) : ServiceBusReceiver
    {
        private long _next = 1;

        public int Received { get; private set; }

        public TimeSpan LockFor { get; init; } = TimeSpan.FromMinutes(5);

        /// <summary>Renewing and settling fail as if the broker dropped the locks.</summary>
        public bool LocksLost { get; init; }

        public System.Collections.Concurrent.ConcurrentBag<long> Abandoned { get; } = [];

        public override Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
            int maxMessages, TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
        {
            var take = (int)Math.Min(Math.Min(maxMessages, batchSize), count - _next + 1);
            var batch = Enumerable.Range(0, Math.Max(0, take))
                .Select(i => ServiceBusModelFactory.ServiceBusReceivedMessage(
                    sequenceNumber: _next + i, lockedUntil: DateTimeOffset.UtcNow + LockFor))
                .ToList();
            _next += batch.Count;
            Received += batch.Count;
            return Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>(batch);
        }

        public override Task AbandonMessageAsync(
            ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default)
        {
            Abandoned.Add(message.SequenceNumber);
            return Task.CompletedTask;
        }

        public override Task RenewMessageLockAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default) =>
            LocksLost ? throw LockLost() : Task.CompletedTask;

        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default) =>
            LocksLost ? throw LockLost() : Task.CompletedTask;

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static ServiceBusException LockLost() => new("lock lost", ServiceBusFailureReason.MessageLockLost);
    }
}
