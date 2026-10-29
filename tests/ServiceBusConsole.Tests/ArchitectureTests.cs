using ServiceBusConsole.Components;

namespace ServiceBusConsole.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void App_UsesTheAbstractionsOnly_NotTheAzureSdk() =>
        typeof(App).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "")
            .ShouldNotContain(name => name.StartsWith("Azure."));
}
