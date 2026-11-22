using System.CommandLine;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.Tests;

public sealed class CommandLineTests
{
    private static RunInput Parse(params string[] args)
    {
        var builder = Host.CreateDefaultBuilder();
        var input = new AppCommands(builder, PluginCatalog.Empty, TestTerms.Transport).Bind(args).ShouldBeOfType<RunInput>();
        input.HostBuilder.ShouldBeSameAs(builder);
        return input;
    }

    /// <summary>What azsb prints for <paramref name="args"/>, and its exit code.</summary>
    public static (string Output, int ExitCode) Invoke(params string[] args)
    {
        var output = new StringWriter();
        var exitCode = new AppCommands(Host.CreateDefaultBuilder(), PluginCatalog.Empty, TestTerms.Transport).Root.Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = output });
        return (output.ToString(), exitCode);
    }

    [Test]
    public void NoArguments_RunTheApp_OnTheConnectionPage() => Parse().StartupTarget.ShouldBeNull();

    [Test]
    [Arguments("--subscription", "--namespace")]
    [Arguments("-s", "-n")]
    public void SubscriptionAndNamespace_AreTheStartupTarget(string subscriptionFlag, string namespaceFlag) =>
        Parse(subscriptionFlag, "Contoso Development", namespaceFlag, "sb-orders-dev").StartupTarget
            .ShouldBe(new StartupTarget("Contoso Development", "sb-orders-dev"));

    [Test]
    [Arguments("--open")]
    [Arguments("-o")]
    public void Open_IsPartOfTheStartupTarget(string flag) =>
        Parse("-s", "Contoso Development", "-n", "sb-orders-dev", flag, "orders/active").StartupTarget
            .ShouldBe(new StartupTarget("Contoso Development", "sb-orders-dev", "orders/active"));

    [Test]
    public async Task OpenWithoutNamespace_IsInvalidUsage() =>
        (await RunCommand.ExecuteAsync(Parse("-s", "Contoso Development", "--open", "orders"))).ShouldBeFalse();

    [Test]
    public void Version_PrintsTheVersion()
    {
        var (output, exitCode) = Invoke("--version");

        exitCode.ShouldBe(0);
        output.Trim().ShouldNotBeEmpty();
    }

    [Test]
    public void RunByName_TakesTheSameFlags() =>
        Parse("run", "--subscription", "emulator").StartupTarget.ShouldBe(new StartupTarget("emulator"));

    [Test]
    [Arguments("--theme")]
    [Arguments("-t")]
    public void Theme_IsTheFlagsValue(string flag) => Parse(flag, "light").Theme.ShouldBe("light");

    [Test]
    public async Task NamespaceWithoutSubscription_IsInvalidUsage() =>
        (await RunCommand.ExecuteAsync(Parse("--namespace", "sb-orders-dev"))).ShouldBeFalse();

    [Test]
    public void UnknownFlag_IsInvalidUsage_AndRunsNothing()
    {
        new AppCommands(Host.CreateDefaultBuilder(), PluginCatalog.Empty, TestTerms.Transport).Bind("--colour", "red").ShouldBeNull();
        var (output, exitCode) = Invoke("--colour", "red");

        exitCode.ShouldNotBe(0);
        output.ShouldContain("--colour");
    }

    [Test]
    [Arguments("help")]
    [Arguments("--help")]
    public void Help_ShowsTheToolName_AndItsCommands(string arg)
    {
        var (output, exitCode) = Invoke(arg);

        exitCode.ShouldBe(0);
        output.ShouldContain("azsb");
        output.ShouldContain("--subscription");
        foreach (var command in new[] { "run", "config", "plugins", "help" }) output.ShouldContain($"  {command} ");
    }

    [Test]
    public void TheTransportsWords_NameTheOptions_WithTheGenericOnesAsAliases()
    {
        var server = new LevelTerm("Server", "Servers", "server", "servers", "🖥", "server", 's', "Server to start in", "Server");
        var vhost = new LevelTerm("Virtual host", "Virtual hosts", "virtual host", "virtual hosts", "🏠", "vhost", 'v', "Virtual host to open", "Virtual host");
        var commands = new AppCommands(Host.CreateDefaultBuilder(), PluginCatalog.Empty,
            TestTerms.Transport with { Terms = TestTerms.ServiceBus with { Connection = server, Broker = vhost } });

        commands.Bind("--server", "local", "--vhost", "/").ShouldBeOfType<RunInput>().StartupTarget.ShouldBe(new StartupTarget("local", "/"));
        commands.Bind("--connection", "local", "-v", "orders").ShouldBeOfType<RunInput>().StartupTarget.ShouldBe(new StartupTarget("local", "orders"));
        commands.Bind("--subscription", "local").ShouldBeNull();
    }
}
