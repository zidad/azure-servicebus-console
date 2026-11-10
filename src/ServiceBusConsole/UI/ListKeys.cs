namespace ServiceBusConsole.UI;

/// <summary>The entity commands of the list pages, so their keys and aliases stay the same everywhere.</summary>
public static class ListKeys
{
    public static KeyBinding Delete(Action run) =>
        new("D", $"{Icons.Delete} {PageStrings.Key_Delete}", run) { Danger = true, Aliases = ["F9", "Delete", "\\"] };

    public static KeyBinding PurgeDlq(Action run) =>
        new("P", $"{Icons.Purge} {PageStrings.Key_PurgeDlq}", run) { Danger = true, Aliases = ["F11"] };
}
