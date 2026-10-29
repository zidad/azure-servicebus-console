namespace ServiceBusConsole.Tests;

internal static class TestRun
{
    /// <summary>The running test's token: cancelled when the test times out or the run is aborted.</summary>
    public static CancellationToken Ct =>
        TestContext.Current?.Execution.CancellationToken
        ?? throw new InvalidOperationException("No test is running on this async flow.");
}
