using JasperFx.CommandLine.Help;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.Tests;

public sealed class CommandLineTests
{
    private static RunInput Parse(params string[] args)
    {
        var builder = Host.CreateDefaultBuilder();
        var run = new AppCommands(builder).BuildRun(args);
        run.Command.ShouldBeOfType<RunCommand>();
        var input = run.Input.ShouldBeOfType<RunInput>();
        input.HostBuilder.ShouldBeSameAs(builder);
        return input;
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
    public void RunByName_TakesTheSameFlags() =>
        Parse("run", "--subscription", "emulator").StartupTarget.ShouldBe(new StartupTarget("emulator"));

    [Test]
    public async Task NamespaceWithoutSubscription_IsInvalidUsage() =>
        (await new RunCommand().Execute(Parse("--namespace", "sb-orders-dev"))).ShouldBeFalse();

    [Test]
    public void Help_ShowsTheToolName() =>
        new AppCommands(Host.CreateDefaultBuilder()).BuildRun(["help"]).Input.ShouldBeOfType<HelpInput>().AppName.ShouldBe("azsb");
}
