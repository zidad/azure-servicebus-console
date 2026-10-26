namespace ServiceBusConsole;

public enum StepState { Pending, Running, Done }

public enum OperationOutcome { Succeeded, Cancelled, Failed }

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

/// <summary>
/// A running operation: a progress dialog for batch work (<see cref="Modal"/>), the status line for loads.
/// <see cref="Cancel"/> asks it to stop; what was done so far is kept.
/// </summary>
public sealed class OperationProgress(string title, IReadOnlyList<OperationStep> steps, bool modal = true)
{
    private readonly CancellationTokenSource _cancel = new();

    public string Title { get; } = title;

    public IReadOnlyList<OperationStep> Steps { get; } = steps;

    public bool Modal { get; } = modal;

    public CancellationToken Token => _cancel.Token;

    public bool Cancelled => _cancel.IsCancellationRequested;

    public void Cancel() => _cancel.Cancel();

    /// <summary>The result line once finished; the dialog then waits for OK.</summary>
    public string? Result { get; private set; }

    public OperationOutcome Outcome { get; private set; }

    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Result))]
    public bool Finished => Result is not null;

    public void Finish(string result, OperationOutcome outcome)
    {
        foreach (var step in Steps.Where(s => s.State == StepState.Running)) step.State = StepState.Done;
        Outcome = outcome;
        Result = result;
    }

    /// <summary>Marks earlier steps done and <paramref name="step"/> running.</summary>
    public OperationStep Start(int step)
    {
        for (var i = 0; i < step; i++) Steps[i].State = StepState.Done;
        Steps[step].State = StepState.Running;
        return Steps[step];
    }
}
