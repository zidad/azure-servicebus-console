using System.Text.RegularExpressions;

namespace ServiceBusConsole.Plugins.Claude;

/// <summary>The run's <c>prompt.md</c>: the <c>prompt</c> setting or <see cref="Default"/>, filled.</summary>
public static partial class ClaudePrompt
{
    public const string Default =
        """
        Analyse an Azure Service Bus message I selected in azsb, a terminal browser for Service Bus.
        Entity: {entity} in namespace {namespace}. Message #{sequenceNumber}, id {messageId}, subject "{subject}".

        The working directory is the codebase that sends or handles these messages: find the producer and the consumer of this entity in it.
        Explain what the message is and what it does in the system.
        If it was dead-lettered, explain the likely cause from the dead-letter reason and description, the delivery count and the consumer code, and suggest fixes.
        Be concise; I'll ask follow-up questions here.

        The message and my azsb session follow; they are also saved as {messageFile} and {contextFile}.

        {message}

        {context}
        """;

    // Filled after the message placeholders, so a body containing "{subject}" stays as it is.
    public static string For(MessageActionContext context, string? prompt, AnalysisRun run) =>
        Inputs().Replace(Template.Fill(prompt ?? Default, context), match => match.Groups[1].Value.ToLowerInvariant() switch
        {
            "messagefile" => run.MessageFile,
            "contextfile" => run.ContextFile,
            "message" => MessageMarkdown.Format(context.Message).Trim(),
            _ => SessionMarkdown.Format(context.Session).Trim(),
        });

    [GeneratedRegex(@"\{(messageFile|contextFile|message|context)\}", RegexOptions.IgnoreCase)]
    private static partial Regex Inputs();
}
