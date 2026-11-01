using System.Text;

namespace ServiceBusConsole.AzureServiceBus.Tests;

public sealed class AzureCliProfileTests
{
    // az writes its profile with a UTF-8 byte order mark.
    private static async Task<string?> CurrentAsync(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"azureProfile-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), Ct);
        try
        {
            return await AzureCliProfile.CurrentSubscriptionIdAsync(path, Ct);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ReadsTheDefaultSubscription() =>
        (await CurrentAsync("""{"subscriptions":[{"id":"a","isDefault":false},{"id":"b","isDefault":true}],"installationId":"x"}"""))
            .ShouldBe("b");

    [Test]
    public async Task WithoutADefault_IsNull() =>
        (await CurrentAsync("""{"subscriptions":[{"id":"a","isDefault":false}]}""")).ShouldBeNull();

    [Test]
    public async Task WithoutAProfile_IsNull() =>
        (await AzureCliProfile.CurrentSubscriptionIdAsync(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"), Ct))
            .ShouldBeNull();
}
