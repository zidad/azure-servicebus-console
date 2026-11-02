using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Tests;

public sealed class PluginCatalogTests
{
    private static readonly MessageInfo Message = new() { SequenceNumber = 7, State = MessageState.Peeked, Subject = "order-created" };
    private static readonly MessageSource Orders = new MessageSource.Queue("orders");
    private static readonly SessionSnapshot Session = new() { At = DateTimeOffset.UnixEpoch };

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, v.Value))).Build();

    private static FakeMessageAction Action(string pluginId, string label, Func<MessageActionContext, bool>? when = null) =>
        new(pluginId, label, (_, _) => Task.FromResult(new ActionResult("done"))) { When = when ?? (_ => true) };

    [Test]
    public void FromAssemblies_CreatesThePluginsTheirAttributesName()
    {
        var catalog = PluginCatalog.FromAssemblies([typeof(FakePlugin).Assembly, typeof(IPlugin).Assembly]);

        catalog.Plugins.Select(p => p.GetType()).ShouldBe([typeof(FakePlugin)]);
        catalog.Find("fake").ShouldBeOfType<FakePlugin>();
        catalog.Find("other").ShouldBeNull();
    }

    [Test]
    public void Discover_LoadsTheBundledPluginAssemblies_NotTheContracts()
    {
        var bundled = Directory.EnumerateFiles(AppContext.BaseDirectory, $"{PluginCatalog.AssemblyPrefix}*.dll").Select(Path.GetFileNameWithoutExtension);
        File.Exists(Path.Combine(AppContext.BaseDirectory, "ServiceBusConsole.Plugins.dll")).ShouldBeTrue();

        PluginCatalog.Discover(AppContext.BaseDirectory).Plugins.Select(p => p.GetType().Assembly.GetName().Name).Distinct().ShouldBe(bundled, ignoreOrder: true);
    }

    [Test]
    public void InvalidOrDuplicateIds_AreRejected()
    {
        Should.Throw<InvalidOperationException>(() => new PluginCatalog([new FakePlugin { Id = "Fake" }])).Message.ShouldContain("lower case");
        Should.Throw<InvalidOperationException>(() => new PluginCatalog([new FakePlugin(), new FakePlugin()])).Message.ShouldContain("'fake'");
    }

    [Test]
    public void Plugins_AreEnabledAsSet_ElseWhenNothingIsMissing()
    {
        var installed = new PluginEnvironment(command => command == "gh", _ => null);
        var needsCli = new FakePlugin { Missing = (settings, env) => env.CommandExists(settings["repo"] ?? "") ? null : $"{settings["repo"]} not found" };
        var catalog = new PluginCatalog([needsCli], installed);

        catalog.Status(needsCli, Config()).ShouldBe(new PluginStatus(false, "zidad/example not found"));
        catalog.Status(needsCli, Config(("plugins:fake:repo", "gh"))).ShouldBe(new PluginStatus(true, null));
        catalog.Status(needsCli, Config(("plugins:fake:enabled", "auto"), ("plugins:fake:repo", "gh"))).Enabled.ShouldBeTrue();
        catalog.Status(needsCli, Config(("plugins:fake:enabled", "true"))).ShouldBe(new PluginStatus(true, null));
        catalog.Status(needsCli, Config(("plugins:fake:enabled", "False"), ("plugins:fake:repo", "gh"))).ShouldBe(new PluginStatus(false, null));
        catalog.Status(new FakePlugin(), Config(("plugins:other:enabled", "false"))).Enabled.ShouldBeTrue();
    }

    [Test]
    public void PluginStatus_SaysWhyItIsOff()
    {
        new PluginStatus(true, null).ToString().ShouldBe("enabled");
        new PluginStatus(false, null).ToString().ShouldBe("disabled");
        new PluginStatus(false, "gh not found").ToString().ShouldBe("disabled (gh not found)");
    }

    [Test]
    public void ConfigKeys_AreEachPluginsEnabledFlagAndSettings_SecretsMarked()
    {
        var keys = new PluginCatalog([new FakePlugin()]).ConfigKeys;

        keys.Select(k => (k.Key, k.Default, k.IsSecret)).ShouldBe(
        [
            ("plugins.fake.enabled", "auto", false),
            ("plugins.fake.repo", "zidad/example", false),
            ("plugins.fake.token", null, true),
        ]);
        keys[1].Description.ShouldBe("Repository to file issues in");
        ConfigKeys.IsSecret("PLUGINS.FAKE.TOKEN", keys).ShouldBeTrue();
        ConfigKeys.IsSecret("plugins.fake.repo", keys).ShouldBeFalse();
    }

    [Test]
    public void Settings_AreThePluginsSectionOverItsDefaults()
    {
        var plugin = new FakePlugin();

        PluginCatalog.Settings(plugin, Config())["repo"].ShouldBe("zidad/example");
        var settings = PluginCatalog.Settings(plugin, Config(("plugins:fake:repo", "acme/orders"), ("plugins:fake:token", "s3cret"), ("plugins:other:repo", "x")));
        settings["repo"].ShouldBe("acme/orders");
        settings["token"].ShouldBe("s3cret");
    }

    [Test]
    public void AddPlugins_RegistersOnlyEnabledPluginsActions()
    {
        var on = new FakePlugin { Id = "on", Actions = [Action("on", "Copy")] };
        var off = new FakePlugin { Id = "off", Actions = [Action("off", "Delete")] };
        var missing = new FakePlugin { Id = "missing", Actions = [Action("missing", "Analyse")], Missing = (_, _) => "tool not found" };
        var services = new ServiceCollection()
            .AddSingleton<IPluginHost>(new FakePluginHost())
            .AddPlugins(new PluginCatalog([on, off, missing]), Config(("plugins:off:enabled", "false")))
            .BuildServiceProvider();

        services.GetServices<IMessageAction>().Select(a => a.Label).ShouldBe(["Copy"]);
        services.GetRequiredService<PluginActions>().For(Message, Orders, Session).Select(a => a.Action.Label).ShouldBe(["Copy"]);
        services.GetRequiredService<IPluginHost>().ShouldBeOfType<FakePluginHost>();
    }

    [Test]
    public void For_OffersWhatCanRun_ByPluginName_AndSkipsUnknownOrFailingActions()
    {
        var host = new FakePluginHost();
        var catalog = new PluginCatalog([new FakePlugin { Id = "zeta", Name = "Zeta" }, new FakePlugin { Id = "alpha", Name = "Alpha" }]);
        var actions = new PluginActions(
            [
                Action("zeta", "Z1"),
                Action("alpha", "A1"),
                Action("alpha", "Dead letters only", c => c.Source.IsDlq),
                Action("alpha", "Broken", _ => throw new InvalidOperationException("bug")),
                Action("gone", "Orphan"),
                Action("alpha", "A2", c => c.Settings["repo"] == "zidad/example" && c.PluginId == "alpha"),
            ],
            catalog, Config(), host);

        actions.For(Message, Orders, Session).Select(a => (a.Plugin.Name, a.Action.Label)).ShouldBe([("Alpha", "A1"), ("Alpha", "A2"), ("Zeta", "Z1")]);
        actions.Any(Message, Orders, () => Session).ShouldBeTrue();
    }

    [Test]
    public void Any_WithoutActions_TakesNoSnapshot()
    {
        var actions = new PluginActions([], PluginCatalog.Empty, Config(), new FakePluginHost());

        actions.Any(Message, Orders, () => throw new InvalidOperationException("snapshot taken")).ShouldBeFalse();
    }
}
