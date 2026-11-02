namespace ServiceBusConsole.Plugins.Linear;

internal sealed record NewIssue(string TeamId, string Title, string Description, IReadOnlyList<string> LabelIds);
