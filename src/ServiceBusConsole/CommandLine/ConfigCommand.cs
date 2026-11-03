using System.Text.Json;
using JasperFx.CommandLine;

namespace ServiceBusConsole.CommandLine;

[Description("Read and write settings in the config file, like git config")]
public sealed class ConfigCommand : JasperFxCommand<ConfigInput>
{
    public ConfigCommand()
    {
        // Member selectors, never evaluated: the coalescing only satisfies their object return type.
        Usage("get <key>: print a value, exit code 1 when unset; unset <key>: remove it").Arguments(x => x.Action, x => x.Key ?? "").ValidFlags();
        Usage("set <key> <value>: true/false and integers are stored as such, the rest as text").Arguments(x => x.Action, x => x.Key ?? "", x => x.Value ?? "").ValidFlags();
        Usage("list: print key=value lines; path: print the config file's path").Arguments(x => x.Action).ValidFlags(x => x.AllFlag);
    }

    public override bool Execute(ConfigInput input)
    {
        try
        {
            return (input.Action, input.Key, input.Value) switch
            {
                (ConfigAction.get, { } key, null) => Get(input, key),
                (ConfigAction.set, { } key, { } value) => Set(input, key, value),
                (ConfigAction.unset, { } key, null) => input.File.Unset(key),
                (ConfigAction.list, null, null) => List(input),
                (ConfigAction.path, null, null) => Print(input, input.File.FilePath),
                _ => PrintUsage(input),
            };
        }
        catch (Exception e) when (e is ArgumentException or JsonException or InvalidDataException or IOException)
        {
            input.Error.WriteLine($"error: {e.Message}");
            return false;
        }
    }

    private static bool Get(ConfigInput input, string key) => input.File.Get(key) is { } value && Print(input, value);

    private static bool Set(ConfigInput input, string key, string value)
    {
        input.File.Set(key, value);
        if (!ConfigKeys.IsKnown(key, input.Keys))
            input.Error.WriteLine($"warning: unknown key '{key}', set anyway");
        return true;
    }

    private static bool List(ConfigInput input)
    {
        var entries = input.File.List();
        var lines = entries.Select(e => $"{e.Key}={e.Value}");
        var unset = input.AllFlag
            ? input.Keys
                .Where(k => !entries.Any(e => string.Equals(e.Key, k.Key, StringComparison.OrdinalIgnoreCase)))
                .Select(k => $"# {k.Key}={k.Default}  ({k.Description})")
            : [];
        foreach (var line in lines.Concat(unset)) input.Out.WriteLine(line);
        return true;
    }

    private static bool Print(ConfigInput input, string text)
    {
        input.Out.WriteLine(text);
        return true;
    }

    private static bool PrintUsage(ConfigInput input)
    {
        input.Error.WriteLine("usage: azsb config get <key> | set <key> <value> | unset <key> | list [--all] | path");
        return false;
    }
}
