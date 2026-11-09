namespace ServiceBusConsole.Tests;

public sealed class StartupDestinationTests
{
    private static readonly JumpTarget Orders = new JumpTarget.Queue(new QueueInfo { Name = "orders" });
    private static readonly JumpTarget Events = new JumpTarget.Topic(new TopicInfo { Name = "events" });
    private static readonly JumpTarget Billing = new JumpTarget.Subscription(new SubscriptionInfo { Name = "billing", TopicName = "events" });
    private static readonly JumpTarget QueueNamedTopics = new JumpTarget.Queue(new QueueInfo { Name = "topics" });

    private static StartupDestination? Find(string path, params JumpTarget[] extra) =>
        StartupDestination.Find(path, [Orders, Events, Billing, .. extra], Section.All);

    [Test]
    [Arguments("orders")]
    [Arguments("ORDERS")]
    [Arguments("/orders/")]
    [Arguments("orders/dlq")]
    public void AQueue_OpensOnDeadLetters(string path) => Find(path).ShouldBe(new StartupDestination.Entity(Orders, Active: false));

    [Test]
    [Arguments("orders/active")]
    [Arguments("orders/Active")]
    public void ActiveSuffix_OpensTheActiveTab(string path) => Find(path).ShouldBe(new StartupDestination.Entity(Orders, Active: true));

    [Test]
    public void TopicAndSubscription_NameASubscription() =>
        Find("events/billing/active").ShouldBe(new StartupDestination.Entity(Billing, Active: true));

    [Test]
    public void ATopic_OpensItsPage_ButHasNoTabs()
    {
        Find("events").ShouldBe(new StartupDestination.Entity(Events, Active: false));
        Find("events/dlq").ShouldBeNull();
    }

    [Test]
    [Arguments("queues", "/queues")]
    [Arguments("Topics", "/topics")]
    [Arguments("subscriptions", "/subscriptions")]
    public void AListName_OpensThatList(string path, string route) =>
        Find(path).ShouldBeOfType<StartupDestination.List>().Section.Route.ShouldBe(route);

    [Test]
    public void AnEntity_WinsOverAListOfTheSameName() =>
        Find("topics", QueueNamedTopics).ShouldBe(new StartupDestination.Entity(QueueNamedTopics, Active: false));

    [Test]
    public void ATopicList_IsOnlyThereWithTopics() =>
        StartupDestination.Find("topics", [Orders], [Section.Queues]).ShouldBeNull();

    [Test]
    [Arguments("ord")]
    [Arguments("orders/archive")]
    [Arguments("missing")]
    public void AnythingElse_IsNotFound(string path) => Find(path).ShouldBeNull();
}
