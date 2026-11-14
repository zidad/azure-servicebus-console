using Microsoft.Extensions.Configuration;
using ServiceBusConsole.AzureServiceBus;

namespace ServiceBusConsole.Tests;

public sealed class AzureSettingsTests
{
    private static AzureOptions Read(params (string Key, string Value)[] settings) => AzureSettings.Read(
        new ConfigurationBuilder().AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value))).Build());

    [Test]
    public void Unset_SignsInWithTheAzureCliOverTcp() =>
        Read().ShouldBe(new AzureOptions { SignIn = AzureSignIn.Cli, Transport = ServiceBusTransport.Tcp });

    [Test]
    public void Values_IgnoreCase() =>
        Read(("azure:auth", "Browser"), ("azure:transport", "websockets"))
            .ShouldBe(new AzureOptions { SignIn = AzureSignIn.Browser, Transport = ServiceBusTransport.WebSockets });

    [Test]
    public void Default_IsTheAzureSdkChain() =>
        Read(("azure:auth", "default")).SignIn.ShouldBe(AzureSignIn.Default);

    [Test]
    [Arguments("azure:auth", "password", "password is not a valid azure.auth: use cli | browser | default")]
    [Arguments("azure:transport", "1", "1 is not a valid azure.transport: use tcp | websockets")]
    public void UnknownValue_IsAUsageError(string key, string value, string message)
    {
        var failure = Should.Throw<CommandFailure>(() => Read((key, value)));

        failure.ExitCode.ShouldBe(ExitCodes.Usage);
        failure.Message.ShouldBe(message);
    }
}
