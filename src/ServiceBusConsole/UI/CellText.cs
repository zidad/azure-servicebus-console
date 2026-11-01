using System.Globalization;

namespace ServiceBusConsole.UI;

public static class CellText
{
    /// <summary><paramref name="text"/> cut to <paramref name="width"/> terminal cells, ending in "…" when cut.</summary>
    public static string Fit(string text, int width)
    {
        if (text.GetCellWidth() <= width) return text;
        var kept = "";
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext() && (kept + elements.GetTextElement()).GetCellWidth() < width)
            kept += elements.GetTextElement();
        return kept.TrimEnd() + "…";
    }
}
