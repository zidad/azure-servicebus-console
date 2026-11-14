namespace ServiceBusConsole.AzureServiceBus;

/// <summary>How azsb signs in to Azure.</summary>
public enum AzureSignIn
{
    /// <summary>The account <c>az login</c> signed in.</summary>
    Cli,

    /// <summary>A browser window, once per run.</summary>
    Browser,

    /// <summary>The Azure SDK's chain: environment variables, workload or managed identity, then developer tools.</summary>
    Default,
}
