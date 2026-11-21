using System.Runtime.CompilerServices;
using EasyNetQ.Management.Client;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.RabbitMQ;

/// <summary>Queues of a virtual host from the management API, a page of 100 per request.</summary>
internal sealed class QueueService(IManagementClient management, string vhost, ILogger<QueueService> logger) : IQueueService
{
    private const int PageSize = 100;

    public async IAsyncEnumerable<QueueInfo> GetQueuesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing queues of {Vhost}", vhost);
        for (var page = 1; ; page++)
        {
            var result = await management.GetQueuesByPageAsync(vhost, new PageCriteria(page, PageSize), ct);
            foreach (var queue in result.Items)
                yield return new QueueInfo { Name = queue.Name, ActiveMessageCount = queue.Messages };
            if (page >= result.PageCount) yield break;
        }
    }
}
