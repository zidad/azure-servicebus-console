namespace ServiceBusConsole.UI;

/// <summary>
/// Remembers which count cells changed in a poll, so the list can highlight them for <see cref="Fade"/>.
/// </summary>
public sealed class CountChanges<T>(Func<T, string> key, params (string Column, Func<T, long> Value)[] columns)
{
    public static readonly TimeSpan Fade = TimeSpan.FromSeconds(2);

    private readonly Dictionary<(string Row, string Column), DateTimeOffset> _changedAt = [];

    public TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>Records every column whose value differs between <paramref name="old"/> and <paramref name="current"/>.</summary>
    public void Compare(T old, T current)
    {
        var now = Time.GetUtcNow();
        foreach (var (column, value) in columns)
            if (value(old) != value(current))
                _changedAt[(key(current), column)] = now;
    }

    /// <summary>Background for a cell: <see cref="Theme.Changed"/> right after a change, fading to none.</summary>
    public Color? Highlight(T row, string column)
    {
        if (!_changedAt.TryGetValue((key(row), column), out var at)) return null;
        var age = (Time.GetUtcNow() - at) / Fade;
        return age >= 1 ? null : Theme.Changed.Blend(Theme.ChangedFadeTo, (float)age);
    }

    /// <summary>Whether a highlight is still fading; drops the finished ones.</summary>
    public bool IsFading()
    {
        var now = Time.GetUtcNow();
        foreach (var cell in _changedAt.Where(c => now - c.Value >= Fade).Select(c => c.Key).ToList())
            _changedAt.Remove(cell);
        return _changedAt.Count > 0;
    }
}
