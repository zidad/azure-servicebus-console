namespace ServiceBusConsole.UI;

/// <summary>
/// Helpers for the <see cref="FullscreenPage"/> layout. Centralises the line-height
/// arithmetic so pages don't have to count chrome by hand.
/// </summary>
public static class FullscreenLayout
{
    /// <summary><see cref="ConnectionHeader"/>: the breadcrumb line and a rule — 2 lines.</summary>
    public const int Header = 2;

    /// <summary><see cref="SectionTabs"/> under the header of a section's list page — 1 line.</summary>
    public const int Tabs = 1;

    /// <summary><c>Padder(0,1,0,0)</c> + a single-line count/status markup — 2 lines.</summary>
    public const int CountRow = 2;

    /// <summary><c>SpectreTable</c> with <c>TableBorder.Rounded</c> — top + header + separator + bottom = 4 lines.</summary>
    public const int RoundedTableChrome = 4;

    /// <summary><c>SpectreTable</c> with <c>TableBorder.Rounded</c> in the message browser — top + header + separator + bottom = 4 lines.</summary>
    public const int SimpleTableChrome = 4;

    /// <summary>Footer button row wrapped in <c>Padder(1,0,0,0)</c> + <c>Columns</c> — 1 line.</summary>
    public const int FooterButtons = 1;

    /// <summary><see cref="MetricsPanel"/>: header line + charts, which take <c>Height + 1</c> lines with axes — 12 lines.</summary>
    public const int MetricsPanel = 1 + MetricsChartHeight + 1;

    /// <summary><see cref="StepPlot.Height"/> of the metrics charts: 8 plot rows.</summary>
    public const int MetricsChartHeight = 10;

    /// <summary>
    /// Calculates <c>Scrollable.PageSize</c> so the table fills the remaining space inside
    /// a <see cref="FullscreenPage"/>. Pass the total non-table chrome in lines; the method
    /// subtracts it from the FlexBox height (<c>terminalHeight - 1</c>) and floors at 5.
    /// </summary>
    /// <param name="chromeLines">Sum of every non-table element's height — e.g.
    /// <c>Header + RoundedTableChrome + FooterButtons</c>.
    /// Include the table's border chrome; the result is the number of data rows.</param>
    public static int PageSize(int terminalHeight, int chromeLines)
        => Math.Max(5, terminalHeight - 1 - chromeLines);

    /// <summary>
    /// PageSize for a page with a <c>TableBorder.Rounded</c> table and only a button bar footer.
    /// The count is in the header. Chrome: Header + RoundedTableChrome + FooterButtons = 8.
    /// </summary>
    public static int PageSizeRoundedTable(int terminalHeight)
        => PageSize(terminalHeight, Header + RoundedTableChrome + FooterButtons);

    /// <summary>
    /// <see cref="PageSizeRoundedTable(int)"/> with the metrics panel above the buttons when <paramref name="metrics"/>,
    /// and <see cref="SectionTabs"/> under the header when <paramref name="tabs"/>.
    /// </summary>
    public static int PageSizeRoundedTable(int terminalHeight, bool metrics, bool tabs = false)
        => PageSize(terminalHeight, Header + (tabs ? Tabs : 0) + RoundedTableChrome + FooterButtons + (metrics ? MetricsPanel : 0));

    /// <summary>
    /// Message browser: list rows on top and the preview of the focused message below, about half each.
    /// </summary>
    public static BrowserSplit Browser(int terminalHeight)
    {
        var available = terminalHeight - 1 - (Header + CountRow + SimpleTableChrome + FooterButtons);
        var rows = Math.Max(3, available / 2);
        return new(rows, Math.Max(6, available - rows));
    }
}

/// <summary>Lines for the message list and for the preview below it.</summary>
public readonly record struct BrowserSplit(int Rows, int Preview);
