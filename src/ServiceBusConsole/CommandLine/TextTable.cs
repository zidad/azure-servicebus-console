namespace ServiceBusConsole.CommandLine;

/// <summary>Plain aligned columns, readable in a terminal and easy to cut or grep; numbers right-aligned.</summary>
public static class TextTable
{
    public static async Task WriteAsync(TextWriter writer, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlySet<int> numeric)
    {
        var widths = headers.Select((h, i) => rows.Select(r => r[i].Length).Prepend(h.Length).Max()).ToList();
        string Line(IReadOnlyList<string> cells) =>
            string.Join("  ", cells.Select((c, i) => numeric.Contains(i) ? c.PadLeft(widths[i]) : c.PadRight(widths[i]))).TrimEnd();
        await writer.WriteLineAsync(Line(headers));
        foreach (var row in rows) await writer.WriteLineAsync(Line(row));
    }
}
