using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Plugins.Linear.Tests;

public sealed class LinearPluginTests
{
    [Test]
    public void Assembly_NamesThePlugin() =>
        typeof(LinearPlugin).Assembly.GetCustomAttributes<ServiceBusConsolePluginAttribute>()
            .Select(a => a.PluginType).ShouldBe([typeof(LinearPlugin)]);

    [Test]
    public void Settings_KeepTheApiKeySecret_AndHaveDefaults()
    {
        var settings = new LinearPlugin().Settings.ToDictionary(s => s.Key);

        settings.Keys.ShouldBe(["apiKey", "team", "labels", "titleTemplate", "includeContext"], ignoreOrder: true);
        settings.Values.Where(s => s.IsSecret).Select(s => s.Key).ShouldBe(["apiKey"]);
        settings["titleTemplate"].Default.ShouldBe("{entity}: {subject}");
        settings["includeContext"].Default.ShouldBe("true");
    }

    [Test]
    public void ConfigureServices_RegistersTheCreateIssueAction()
    {
        var services = new ServiceCollection();

        new LinearPlugin().ConfigureServices(services);

        services.Where(d => d.ServiceType == typeof(IMessageAction)).ShouldHaveSingleItem();
    }

    [Test]
    public void Action_IsOfferedForEveryMessage_WithAWideEmoji()
    {
        var action = new CreateIssueAction(new LinearClient(new HttpClient(new FakeLinearHandler())), _ => null);

        action.PluginId.ShouldBe("linear");
        action.Label.ShouldBe("Create Linear issue");
        action.Icon.ShouldNotContain('️');
        action.CanRun(LinearTestContext.Create(new Dictionary<string, string?>())).ShouldBeTrue();
    }

    [Test]
    public void IsOnByDefault_WithAnApiKeySetOrInTheEnvironment()
    {
        var plugin = new LinearPlugin();
        IConfiguration Settings(string? key) => new ConfigurationBuilder().AddInMemoryCollection([KeyValuePair.Create("apiKey", key)]).Build();
        PluginEnvironment Variable(string? value) => new(_ => false, name => name == "LINEAR_API_KEY" ? value : null);

        plugin.Missing(Settings("lin_api_1"), Variable(null)).ShouldBeNull();
        plugin.Missing(Settings(null), Variable("lin_api_2")).ShouldBeNull();
        plugin.Missing(Settings(""), Variable("")).ShouldBe("no API key; set plugins.linear.apiKey or LINEAR_API_KEY");
    }
}
