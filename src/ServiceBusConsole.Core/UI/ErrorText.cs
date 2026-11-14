namespace ServiceBusConsole.Core.UI;

/// <summary>What an error dialog says about an exception.</summary>
public static class ErrorText
{
    // Service Bus puts this in both its HTTP (admin) and AMQP errors when the namespace firewall rejects the caller.
    private const string FirewallDenial = "Ip has been prevented to connect to the endpoint";

    public static string Firewall => UIStrings.ErrorText_Firewall;

    public static string Of(Exception ex) => IsFirewall(ex) ? Firewall : ex.Message;

    public static bool IsFirewall(Exception ex) =>
        Chain(ex).Any(e => e.Message.Contains(FirewallDenial, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<Exception> Chain(Exception? ex) => ex is null ? [] : [ex, .. Chain(ex.InnerException)];
}
