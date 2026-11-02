namespace ServiceBusConsole.CommandLine;

// Lower case: JasperFx parses enum arguments case-sensitively, so these are the words typed.
public enum ConfigAction
{
    get,
    set,
    unset,
    list,
    path,
}
