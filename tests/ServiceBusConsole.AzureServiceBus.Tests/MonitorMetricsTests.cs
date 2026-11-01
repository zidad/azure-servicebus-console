using Azure.ResourceManager.Monitor.Models;

namespace ServiceBusConsole.AzureServiceBus.Tests;

public sealed class MonitorMetricsTests
{
    private static readonly MetricsWindow Hour = new(
        new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero), TimeSpan.FromMinutes(15));

    private static DateTimeOffset At(int minute) => Hour.From.AddMinutes(minute);

    private static MonitorMetric Metric(string name, params IEnumerable<IEnumerable<MonitorMetricValue>> series) =>
        ArmMonitorModelFactory.MonitorMetric(
            id: $"/metrics/{name}", metricType: "Microsoft.Insights/metrics", name: ArmMonitorModelFactory.MonitorLocalizableString(name, name),
            displayDescription: "", errorCode: "Success", errorMessage: null, unit: MonitorMetricUnit.Count,
            timeseries: series.Select(values => ArmMonitorModelFactory.MonitorTimeSeriesElement([], values)));

    private static MonitorMetricValue Value(int minute, double? total = null, double? maximum = null) =>
        ArmMonitorModelFactory.MonitorMetricValue(At(minute), average: null, minimum: null, maximum: maximum, total: total, count: null);

    [Test]
    public void ToMessageMetrics_TakesTotalsForThroughput_AndMaximaForBacklog()
    {
        var metrics = MonitorMetrics.ToMessageMetrics(Hour,
        [
            Metric("IncomingMessages", [Value(0, total: 5, maximum: 99), Value(15, total: 7)]),
            Metric("OutgoingMessages", [Value(0, total: 4)]),
            Metric("ActiveMessages", [Value(30, total: 99, maximum: 12)]),
            Metric("DeadletteredMessages", [Value(45, maximum: 3)]),
        ]);

        metrics.Window.ShouldBe(Hour);
        metrics.Incoming.ShouldBe([new(At(0), 5), new(At(15), 7), new(At(30), 0), new(At(45), 0)]);
        metrics.Outgoing.Select(p => p.Value).ShouldBe([4, 0, 0, 0]);
        metrics.Active.Select(p => p.Value).ShouldBe([0, 0, 12, 0]);
        metrics.DeadLettered.Select(p => p.Value).ShouldBe([0, 0, 0, 3]);
    }

    [Test]
    public void ToMessageMetrics_FillsAMissingMetricWithZeros_AndDropsValuesOutsideTheWindow()
    {
        var metrics = MonitorMetrics.ToMessageMetrics(Hour, [Metric("IncomingMessages", [Value(-15, total: 9), Value(60, total: 9), Value(15, total: 1)])]);

        metrics.Incoming.Select(p => p.Value).ShouldBe([0, 1, 0, 0]);
        metrics.Active.Select(p => p.Value).ShouldBe([0, 0, 0, 0]);
    }

    [Test]
    public void ToMessageMetrics_AddsUpSeveralTimeSeries() =>
        MonitorMetrics.ToMessageMetrics(Hour, [Metric("OutgoingMessages", [Value(0, total: 2)], [Value(0, total: 3)])])
            .Outgoing[0].Value.ShouldBe(5);

    [Test]
    public void Filter_SelectsTheEntity()
    {
        MonitorMetrics.Filter(new MetricsScope.Namespace()).ShouldBeNull();
        MonitorMetrics.Filter(new MetricsScope.Entity("orders")).ShouldBe("EntityName eq 'orders'");
        MonitorMetrics.Filter(new MetricsScope.Entity("datahub.auditstore/iskipaudit")).ShouldBe("EntityName eq 'datahub.auditstore/iskipaudit'");
    }

    [Test]
    public void Queries_AskOneAggregationEach_OverTheWholeWindow()
    {
        var queries = MonitorMetrics.Queries(new MetricsScope.Entity("orders"), Hour);

        queries.Select(q => (q.Metricnames, q.Aggregation)).ShouldBe(
            [("IncomingMessages,OutgoingMessages", "Total"), ("ActiveMessages,DeadletteredMessages", "Maximum")]);
        queries.ShouldAllBe(q => q.Timespan == "2026-11-01T09:00:00.0000000Z/2026-11-01T10:00:00.0000000Z"
                                 && q.Interval == TimeSpan.FromMinutes(15)
                                 && q.Filter == "EntityName eq 'orders'");
    }
}
