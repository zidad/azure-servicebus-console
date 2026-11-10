using System.Text.Json;

namespace ServiceBusConsole.CommandLine;

/// <summary>Reads and writes settings in the config file, like git config.</summary>
public static class ConfigCommand
{
    public static bool Execute(ConfigInput input)
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
            input.Error.WriteLine(CommandLineStrings.FormatConfigCommand_Error(e.Message));
            return false;
        }
    }

    private static bool Get(ConfigInput input, string key) => input.File.Get(key) is { } value && Print(input, value);

    private static bool Set(ConfigInput input, string key, string value)
    {
        input.File.Set(key, value);
        if (!ConfigKeys.IsKnown(key, input.Keys))
            input.Error.WriteLine(CommandLineStrings.FormatConfigCommand_UnknownKey(key));
        return true;
    }

    private static bool List(ConfigInput input)
    {
        var entries = input.File.List();
        var lines = entries.Select(e => $"{e.Key}={(ConfigKeys.IsSecret(e.Key, input.Keys) ? ConfigKey.Mask : e.Value)}");
        var unset = input.All
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
        input.Error.WriteLine(CommandLineStrings.ConfigCommand_Usage);
        return false;
    }
}
