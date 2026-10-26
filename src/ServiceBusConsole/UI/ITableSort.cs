namespace ServiceBusConsole.UI;

public interface ITableSort
{
    SortDirection DirectionOf(string column);
    void Cycle(string column);
}
