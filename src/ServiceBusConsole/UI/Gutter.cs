namespace ServiceBusConsole.UI;

/// <summary>
/// Cell padding for a one-cell gutter column (dead-letter mark, row checkbox) and the column after it:
/// the table's blank column gap is the only space between them.
/// </summary>
public static class Gutter
{
    public const int Width = 2;
    public static readonly Padding Header = new(1, 0, 0, 0);
    public const string Cell = "1,0,0,0";
    public static readonly Padding NextHeader = new(0, 0, 1, 0);
    public const string NextCell = "0,0,1,0";
}
