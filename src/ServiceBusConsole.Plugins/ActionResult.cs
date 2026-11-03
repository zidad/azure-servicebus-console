namespace ServiceBusConsole.Plugins;

/// <summary>What an action did, shown when it finishes and recorded in the session's activity.</summary>
/// <param name="Text">One line, e.g. <c>Created issue #12</c>.</param>
/// <param name="Link">A URL the user can open from the result, e.g. the issue.</param>
/// <param name="FilePath">A file the action wrote, which the user can open.</param>
public sealed record ActionResult(string Text, Uri? Link = null, string? FilePath = null);
