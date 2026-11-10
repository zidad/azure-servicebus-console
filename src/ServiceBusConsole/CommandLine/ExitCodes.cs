namespace ServiceBusConsole.CommandLine;

/// <summary>What the namespace commands exit with, for scripts.</summary>
public static class ExitCodes
{
    public const int Ok = 0;
    public const int Failed = 1;
    public const int Usage = 2;

    /// <summary>The subscription, namespace, entity or a requested message isn't there.</summary>
    public const int NotFound = 3;
}
