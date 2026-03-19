using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class SBClient
{
    private readonly AzureCliCredential _credential;
    private readonly ArmClient _armClient;
    private readonly ILogger<SBClient> _logger;
    private ServiceBusAdministrationClient? _adminClient;
    private ServiceBusClient? _busClient;

    public SBClient(AzureCliCredential credential, ArmClient armClient, ILogger<SBClient> logger)
    {
        _credential = credential;
        _armClient = armClient;
        _logger = logger;
    }

    public async Task<List<NamespaceInfo>> GetNamespacesAsync(string subscriptionId)
    {
        _logger.LogInformation("Listing namespaces for subscription {SubscriptionId}", subscriptionId);
        var namespaces = new List<NamespaceInfo>();
        var sub = _armClient.GetSubscriptionResource(new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}"));

        await foreach (var ns in sub.GetServiceBusNamespacesAsync())
        {
            _logger.LogDebug("Found namespace: {Name}", ns.Data.Name);
            namespaces.Add(new NamespaceInfo
            {
                Name = ns.Data.Name,
                FullyQualifiedNamespace = $"{ns.Data.Name}.servicebus.windows.net",
                ResourceGroup = ns.Id?.ResourceGroupName ?? "",
                Location = ns.Data.Location.DisplayName ?? ns.Data.Location.Name
            });
        }

        _logger.LogInformation("Found {Count} namespaces", namespaces.Count);
        return namespaces;
    }

    public void SetNamespace(string fullyQualifiedNamespace)
    {
        _logger.LogInformation("Connecting to namespace {Namespace}", fullyQualifiedNamespace);
        _adminClient = new ServiceBusAdministrationClient(
            $"https://{fullyQualifiedNamespace}", _credential);
        _busClient = new ServiceBusClient(
            $"sb://{fullyQualifiedNamespace}/", _credential);
    }

    public async Task<List<QueueInfo>> GetQueuesAsync()
    {
        if (_adminClient is null) throw new InvalidOperationException("No namespace selected");

        _logger.LogInformation("Listing queues");
        var queues = new List<QueueInfo>();
        await foreach (var page in _adminClient.GetQueuesAsync().AsPages())
        {
            foreach (var queue in page.Values)
            {
                var runtime = await _adminClient.GetQueueRuntimePropertiesAsync(queue.Name);
                _logger.LogDebug("Queue {Name}: Active={Active}, DLQ={Dlq}", queue.Name, runtime.Value.ActiveMessageCount, runtime.Value.DeadLetterMessageCount);
                queues.Add(new QueueInfo
                {
                    Name = queue.Name,
                    ActiveMessageCount = runtime.Value.ActiveMessageCount,
                    DeadLetterMessageCount = runtime.Value.DeadLetterMessageCount,
                    ScheduledMessageCount = runtime.Value.ScheduledMessageCount,
                    TransferMessageCount = runtime.Value.TransferMessageCount
                });
            }
        }
        _logger.LogInformation("Found {Count} queues", queues.Count);
        return queues;
    }

    public async Task<List<TopicInfo>> GetTopicsWithCountsAsync()
    {
        if (_adminClient is null) throw new InvalidOperationException("No namespace selected");

        _logger.LogInformation("Listing topics");
        var topics = new List<TopicInfo>();
        await foreach (var page in _adminClient.GetTopicsAsync().AsPages())
        {
            foreach (var topic in page.Values)
            {
                var runtime = await _adminClient.GetTopicRuntimePropertiesAsync(topic.Name);
                topics.Add(new TopicInfo
                {
                    Name = topic.Name,
                    SubscriptionCount = runtime.Value.SubscriptionCount,
                    ScheduledMessageCount = runtime.Value.ScheduledMessageCount
                });
            }
        }
        _logger.LogInformation("Found {Count} topics", topics.Count);
        return topics;
    }

    public async Task<List<SubscriptionInfo>> GetSubscriptionsAsync(string topicName)
    {
        if (_adminClient is null) throw new InvalidOperationException("No namespace selected");

        _logger.LogInformation("Listing subscriptions for topic {Topic}", topicName);
        var subscriptions = new List<SubscriptionInfo>();
        await foreach (var page in _adminClient.GetSubscriptionsAsync(topicName).AsPages())
        {
            foreach (var sub in page.Values)
            {
                var runtimeProps = await _adminClient.GetSubscriptionRuntimePropertiesAsync(topicName, sub.SubscriptionName);
                subscriptions.Add(new SubscriptionInfo
                {
                    Name = sub.SubscriptionName,
                    ActiveMessageCount = runtimeProps.Value.ActiveMessageCount,
                    DeadLetterMessageCount = runtimeProps.Value.DeadLetterMessageCount,
                    TransferMessageCount = runtimeProps.Value.TransferMessageCount
                });
            }
        }
        _logger.LogInformation("Found {Count} subscriptions for {Topic}", subscriptions.Count, topicName);
        return subscriptions;
    }

    public async Task<List<MessageInfo>> PeekMessagesAsync(string queueName, int count, bool fromDlq = false)
    {
        if (_busClient is null) throw new InvalidOperationException("No namespace selected");

        _logger.LogInformation("Peeking {Count} messages from {Queue} (DLQ={Dlq})", count, queueName, fromDlq);
        await using var receiver = _busClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            SubQueue = fromDlq ? SubQueue.DeadLetter : SubQueue.None
        });

        var messages = await receiver.PeekMessagesAsync(count);
        _logger.LogInformation("Peeked {Count} messages", messages.Count);
        return messages.Select(MapMessage).ToList();
    }

    public async Task<List<MessageInfo>> ReceiveMessagesAsync(string queueName, int count, bool fromDlq = false)
    {
        if (_busClient is null) throw new InvalidOperationException("No namespace selected");

        _logger.LogWarning("Receiving (destructive) {Count} messages from {Queue} (DLQ={Dlq})", count, queueName, fromDlq);
        await using var receiver = _busClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
            SubQueue = fromDlq ? SubQueue.DeadLetter : SubQueue.None
        });

        var messages = await receiver.ReceiveMessagesAsync(count, TimeSpan.FromSeconds(5));
        _logger.LogInformation("Received {Count} messages", messages.Count);
        return messages.Select(MapMessage).ToList();
    }

    private static MessageInfo MapMessage(ServiceBusReceivedMessage m) => new()
    {
        MessageId = m.MessageId,
        SequenceNumber = m.SequenceNumber,
        EnqueuedTime = m.EnqueuedTime,
        ContentType = m.ContentType ?? "",
        Subject = m.Subject ?? "",
        Body = m.Body?.ToString() ?? "",
        DeliveryCount = m.DeliveryCount,
        Properties = m.ApplicationProperties.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value?.ToString() ?? "")
    };
}

public class NamespaceInfo
{
    public required string Name { get; set; }
    public required string FullyQualifiedNamespace { get; set; }
    public required string ResourceGroup { get; set; }
    public required string Location { get; set; }
}

public class QueueInfo
{
    public required string Name { get; set; }
    public long ActiveMessageCount { get; set; }
    public long DeadLetterMessageCount { get; set; }
    public long ScheduledMessageCount { get; set; }
    public long TransferMessageCount { get; set; }
}

public class TopicInfo
{
    public required string Name { get; set; }
    public int SubscriptionCount { get; set; }
    public long ScheduledMessageCount { get; set; }
}

public class SubscriptionInfo
{
    public required string Name { get; set; }
    public long ActiveMessageCount { get; set; }
    public long DeadLetterMessageCount { get; set; }
    public long TransferMessageCount { get; set; }
}

public class MessageInfo
{
    public required string MessageId { get; set; }
    public long SequenceNumber { get; set; }
    public DateTimeOffset EnqueuedTime { get; set; }
    public string ContentType { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public int DeliveryCount { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}
