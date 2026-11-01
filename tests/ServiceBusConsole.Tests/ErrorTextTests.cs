namespace ServiceBusConsole.Tests;

public sealed class ErrorTextTests
{
    public const string FirewallResponse =
        "Service request failed.\nStatus: 401 (Unauthorized)\n\nContent:\n"
        + "<Error><Code>401</Code><Detail>Ip has been prevented to connect to the endpoint.&#xD;</Detail></Error>";

    [Test]
    public void FirewallDenial_IsExplained() =>
        ErrorText.Of(new InvalidOperationException(FirewallResponse)).ShouldBe(ErrorText.Firewall);

    [Test]
    public void FirewallDenial_InAnInnerException_IsExplained() =>
        ErrorText.Of(new InvalidOperationException("Receive failed.", new UnauthorizedAccessException(FirewallResponse)))
            .ShouldBe(ErrorText.Firewall);

    [Test]
    public void OtherErrors_KeepTheirMessage() =>
        ErrorText.Of(new InvalidOperationException("boom")).ShouldBe("boom");
}
