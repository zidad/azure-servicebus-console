namespace LLMAgentTUI.Components;

/// <summary>
/// Helpers for the <see cref="FullscreenPage"/> layout. Centralises the line-height
/// arithmetic so pages don't have to count chrome by hand.
/// </summary>
public static class FullscreenLayout
{
    /// <summary>Header <c>Panel</c> with default Square border — 3 lines.</summary>
    public const int Header = 3;

    /// <summary><c>Padder(0,1,0,0)</c> + a single-line count/status markup — 2 lines.</summary>
    public const int CountRow = 2;

    /// <summary><c>SpectreTable</c> with <c>TableBorder.Rounded</c> — top + header + separator + bottom = 4 lines.</summary>
    public const int RoundedTableChrome = 4;

    /// <summary><c>SpectreTable</c> with <c>TableBorder.Simple</c> — header row + separator = 2 lines.</summary>
    public const int SimpleTableChrome = 2;

    /// <summary>A footer <c>TextInput</c> (default Rounded border, no outer padder) — 3 lines.</summary>
    public const int FooterTextInput = 3;

    /// <summary>Footer button row wrapped in <c>Padder(1,0,0,0)</c> + <c>Columns</c> — 1 line.</summary>
    public const int FooterButtons = 1;

    /// <summary>
    /// Calculates <c>Scrollable.PageSize</c> so the table fills the remaining space inside
    /// a <see cref="FullscreenPage"/>. Pass the total non-table chrome in lines; the method
    /// subtracts it from the FlexBox height (<c>Console.WindowHeight - 1</c>) and floors at 5.
    /// </summary>
    /// <param name="chromeLines">Sum of every non-table element's height — e.g.
    /// <c>Header + CountRow + RoundedTableChrome + FooterTextInput + FooterButtons</c>.
    /// Include the table's border chrome; the result is the number of data rows.</param>
    public static int PageSize(int chromeLines)
        => Math.Max(5, Console.WindowHeight - 1 - chromeLines);

    /// <summary>
    /// PageSize for a page with a <c>TableBorder.Rounded</c> table and a filter <c>TextInput</c>
    /// in the footer. Chrome: Header + CountRow + RoundedTableChrome + FooterTextInput + FooterButtons = 13.
    /// </summary>
    public static int PageSizeRoundedTableWithFilter()
        => PageSize(Header + CountRow + RoundedTableChrome + FooterTextInput + FooterButtons);

    /// <summary>
    /// PageSize for a page with a <c>TableBorder.Rounded</c> table and only a button bar footer.
    /// Chrome: Header + CountRow + RoundedTableChrome + FooterButtons = 10.
    /// </summary>
    public static int PageSizeRoundedTable()
        => PageSize(Header + CountRow + RoundedTableChrome + FooterButtons);

    /// <summary>
    /// PageSize for a page with a <c>TableBorder.Simple</c> table and only a button bar footer.
    /// Chrome: Header + CountRow + SimpleTableChrome + FooterButtons = 8.
    /// </summary>
    public static int PageSizeSimpleTable()
        => PageSize(Header + CountRow + SimpleTableChrome + FooterButtons);
}
