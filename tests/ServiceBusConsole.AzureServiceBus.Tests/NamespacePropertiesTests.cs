namespace ServiceBusConsole.AzureServiceBus.Tests;

public sealed class NamespacePropertiesTests
{
    [Test]
    public void Of_SkipsUnknown_MarksTheStatus_AndListsNoResourceId()
    {
        var properties = NamespaceProperties.Of("Standard", "West Europe", "", "Disabled", "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/sb");

        properties.Select(p => p.Id).ShouldBe(["sku", "location", "status", "resourceId"]);
        properties.Single(p => p.Id == "status").Healthy.ShouldBe(false);
        properties.Where(p => !p.Listed).Select(p => p.Id).ShouldBe(["resourceId"]);
        NamespaceProperties.Of(null, null, null, "Active", null).Single().Healthy.ShouldBe(true);
    }
}
