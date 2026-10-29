namespace ServiceBusConsole.Tests;

public sealed class MessageSourceTests
{
    [Fact]
    public void ASubscription_SendsToItsTopic()
    {
        MessageSource source = new MessageSource.Subscription("orders", "billing", IsDlq: true);

        source.SendTarget.ShouldBe("orders");
        source.DisplayName.ShouldBe("orders/billing DLQ");
        source.Match(_ => "queue", s => s.Name).ShouldBe("billing");
    }

    [Fact]
    public void SwitchingToTheActiveQueue_KeepsTheKindOfSource()
    {
        MessageSource dlq = new MessageSource.Subscription("orders", "billing", IsDlq: true);

        var active = dlq with { IsDlq = false };

        active.ShouldBe(new MessageSource.Subscription("orders", "billing"));
        active.ShouldNotBe(new MessageSource.Queue("billing"));
    }
}
