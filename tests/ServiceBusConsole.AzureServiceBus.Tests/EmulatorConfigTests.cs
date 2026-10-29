namespace ServiceBusConsole.AzureServiceBus.Tests;

public sealed class EmulatorConfigTests
{
    [Test]
    public void ParsesTheRepositoryEmulatorConfig()
    {
        var config = EmulatorConfigReader.Parse(File.ReadAllText(RepositoryFile("emulator", "Config.json"))).ShouldNotBeNull();

        config.Queues.ShouldNotBeEmpty();
        config.Queues.ShouldAllBe(q => q.Name.Length > 0);
        config.Topics.SelectMany(t => t.Subscriptions).ShouldNotBeEmpty();
    }

    [Test]
    public void MissingListsAreEmpty()
    {
        var config = EmulatorConfigReader.Parse("""{ "UserConfig": { "Namespaces": [ { "Name": "sbemulatorns" } ] } }""").ShouldNotBeNull();

        config.Queues.ShouldBeEmpty();
        config.Topics.ShouldBeEmpty();
    }

    private static string RepositoryFile(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Path.Combine([dir.FullName, .. parts]) is var path && File.Exists(path))
                return path;
        throw new FileNotFoundException(Path.Combine(parts));
    }
}
