using ServiceBusConsole.Components;

namespace ServiceBusConsole.Tests;

public sealed class ArchitectureTests
{
    private static IEnumerable<string> References(Type type) => type.Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");

    [Test]
    public void App_UsesTheAbstractionsOnly_NotTheAzureSdk() =>
        References(typeof(App)).ShouldNotContain(name => name.StartsWith("Azure."));

    [Test]
    public void App_UsesThePluginContractsOnly_NotAPlugin()
    {
        References(typeof(App)).ShouldContain("ServiceBusConsole.Plugins");
        References(typeof(App)).ShouldNotContain(name => name.StartsWith(PluginCatalog.AssemblyPrefix));
    }

    [Test]
    public void PluginContracts_ReferenceNeitherAzureNorTheUi() =>
        References(typeof(IPlugin)).ShouldNotContain(name =>
            name.StartsWith("Azure.") || name.StartsWith("RazorConsole") || name.StartsWith("Spectre.") || name == typeof(App).Assembly.GetName().Name);

    [Test]
    public void Core_IsFreeOfTheTerminalUiAndTheAzureSdk() =>
        References(typeof(MessageBrowser)).ShouldNotContain(name =>
            name.StartsWith("Azure.") || name.StartsWith("RazorConsole") || name.StartsWith("Spectre.")
            || name.StartsWith("Microsoft.AspNetCore") || name == typeof(App).Assembly.GetName().Name);
}
