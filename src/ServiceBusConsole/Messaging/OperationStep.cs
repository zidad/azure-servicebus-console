namespace ServiceBusConsole.Messaging;

/// <summary>One step of a batch operation, e.g. "Requeue to orders" with 7 of 10 done.</summary>
public sealed class OperationStep(string name, int total)
{
    private int _done;

    public string Name { get; } = name;

    /// <summary>Items this step handles; 0 when unknown (purge).</summary>
    public int Total { get; set; } = total;

    public int Done => Volatile.Read(ref _done);

    public StepState State { get; set; }

    public void Report(int done) => Volatile.Write(ref _done, done);

    public int Increment() => Interlocked.Increment(ref _done);
}
