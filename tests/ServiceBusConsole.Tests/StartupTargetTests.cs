namespace ServiceBusConsole.Tests;

public sealed class StartupTargetTests
{
    [Test]
    [Arguments("Contoso Development")]
    [Arguments("contoso development")]
    [Arguments("6F1D2C3B-0000-4000-8000-000000000001")]
    public void Subscription_MatchesByNameOrId_IgnoringCase(string subscription) =>
        new StartupTarget(subscription).Matches(AppHarness.AzureSubscription).ShouldBeTrue();

    [Test]
    [Arguments("Contoso")]
    [Arguments("emulator")]
    public void Subscription_OtherNames_DoNotMatch(string subscription) =>
        new StartupTarget(subscription).Matches(AppHarness.AzureSubscription).ShouldBeFalse();

    [Test]
    [Arguments("Local Emulator")]
    [Arguments("emulator")]
    [Arguments("Emulator")]
    public void Emulator_MatchesByNameOrAsEmulator(string subscription) =>
        new StartupTarget(subscription).Matches(AzureSubscription.Emulator).ShouldBeTrue();

    [Test]
    [Arguments("sb-orders-dev")]
    [Arguments("SB-Orders-Dev")]
    [Arguments("sb-orders-dev.servicebus.windows.net")]
    public void Namespace_MatchesByNameOrHostName_IgnoringCase(string ns) =>
        new StartupTarget("Contoso Development", ns).Matches(AppHarness.AzureNamespace).ShouldBeTrue();

    [Test]
    [Arguments("sb-orders")]
    [Arguments(null)]
    public void Namespace_OtherOrNone_DoesNotMatch(string? ns) =>
        new StartupTarget("Contoso Development", ns).Matches(AppHarness.AzureNamespace).ShouldBeFalse();
}
