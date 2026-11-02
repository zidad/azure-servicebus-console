namespace ServiceBusConsole.Plugins;

/// <summary>Something a plugin does with one message, offered under F2 Actions on the message browser and detail page.</summary>
public interface IMessageAction
{
    /// <summary>The <see cref="IPlugin.Id"/> it belongs to.</summary>
    string PluginId { get; }

    string Id { get; }

    string Label { get; }

    /// <summary>An emoji with default emoji presentation (two cells wide everywhere), not one that needs U+FE0F.</summary>
    string Icon { get; }

    /// <summary>Whether the action is offered for the message; cheap, no I/O.</summary>
    bool CanRun(MessageActionContext context);

    /// <summary>Runs it; throw to report a failure. Cancelled when the user presses Esc.</summary>
    Task<ActionResult> RunAsync(MessageActionContext context, CancellationToken ct);
}
