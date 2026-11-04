using System.Text;

namespace ServiceBusConsole.Services;

/// <summary>
/// Puts text on the terminal's clipboard with OSC 52, which also works over SSH; a terminal without it ignores the
/// sequence. <see cref="Write"/> sends it to the terminal (tests capture it).
/// </summary>
public sealed class Clipboard
{
    public Action<string> Write { get; init; } = text => { Console.Out.Write(text); Console.Out.Flush(); };

    public void Copy(string text) => Write($"\u001b]52;c;{Convert.ToBase64String(Encoding.UTF8.GetBytes(text))}\u0007");
}
