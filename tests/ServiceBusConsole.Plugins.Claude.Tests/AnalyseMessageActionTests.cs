using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Plugins.Claude.Tests;

public sealed class AnalyseMessageActionTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 2, 21, 30, 15, 123, TimeSpan.Zero);

    private static readonly MessageInfo Message = new()
    {
        SequenceNumber = 42,
        State = MessageState.Peeked,
        MessageId = "msg-42",
        Subject = "order-created",
        DeliveryCount = 10,
        DeadLetterReason = "MaxDeliveryCountExceeded",
        Body = Encoding.UTF8.GetBytes("""{"orderId":7}"""),
    };

    private static readonly MessageSource Source = new MessageSource.Queue("orders", IsDlq: true);

    private readonly FakePluginHost _host = new();
    private readonly RecordingProgress _progress = new();
    private readonly string _code;

    public AnalyseMessageActionTests() => _code = Directory.CreateDirectory(Path.Combine(_host.StateDirectory, "code")).FullName;

    public void Dispose() => _host.Dispose();

    private string RunDirectory => Path.Combine(_host.StateDirectory, "plugins", "claude", "orders-dlq-42-20261102-213015123");

    private MessageActionContext Context(params (string Key, string? Value)[] settings)
    {
        var plugin = new ClaudePlugin();
        return new MessageActionContext
        {
            PluginId = plugin.Id,
            Message = Message,
            Source = Source,
            Session = new SessionSnapshot
            {
                At = At,
                Namespace = new NamespaceInfo { Name = "sb-orders-dev", FullyQualifiedNamespace = "sb-orders-dev.servicebus.windows.net", ResourceGroup = "rg", Location = "West Europe" },
                Source = Source,
                Mode = "Peek",
                Message = Message,
                Activity = [new ActivityEntry(At.AddMinutes(-1), ActivityKind.Navigated, "Opened orders DLQ", Source)],
            },
            Settings = new ConfigurationBuilder()
                .AddInMemoryCollection(plugin.Settings.Where(s => s.Default is not null).Select(s => KeyValuePair.Create(s.Key, s.Default)))
                .AddInMemoryCollection([KeyValuePair.Create<string, string?>(ClaudeSettings.WorkingDirectoryKey, _code)])
                .AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, s.Value)))
                .Build(),
            Host = _host,
            Progress = _progress,
        };
    }

    private static ClaudePlatform Mac(params (string Name, string Value)[] variables) =>
        new(ClaudeOs.MacOS, name => variables.FirstOrDefault(v => v.Name == name).Value, command => command == "claude" ? "/usr/local/bin/claude" : null);

    private static AnalyseMessageAction Action(ClaudePlatform? platform = null) => new("claude", platform ?? Mac());

    private string File(string name) => Path.Combine(RunDirectory, name);

    private string ReadPrompt => $"Read {File("prompt.md")} and follow its instructions.";

    private string InlinePrompt => $"\"$(cat '{File("prompt.md")}')\"";

    [Test]
    public async Task Run_WritesTheMessageContextAndPrompt_AndOpensTheirFolder()
    {
        var context = Context();

        var result = await Action().RunAsync(context, Ct);

        (await System.IO.File.ReadAllTextAsync(File("message.json"), Ct)).ShouldBe(MessageJson.Format(Message));
        (await System.IO.File.ReadAllTextAsync(File("context.json"), Ct)).ShouldBe(SessionJson.Format(context.Session));
        var prompt = await System.IO.File.ReadAllTextAsync(File("prompt.md"), Ct);
        prompt.ShouldContain(File("message.json"));
        prompt.ShouldContain(File("context.json"));
        prompt.ShouldContain("Entity: orders DLQ in namespace sb-orders-dev. Message #42, id msg-42, subject \"order-created\".");
        prompt.ShouldContain("dead-lettered");
        prompt.ShouldContain(MessageMarkdown.Format(Message).Trim());
        prompt.ShouldContain(SessionMarkdown.Format(context.Session).Trim());
        prompt.ShouldNotContain("{message}");
        result.ShouldBe(new ActionResult($"Claude Code opened in a new window, in {_code}", FilePath: File("prompt.md")));
        _progress.Reports.ShouldBe([$"Opening Claude Code in {_code}…"]);
    }

    [Test]
    public async Task Run_OpensATerminalWindow_RunningInteractiveClaudeInTheCodebase_ThePromptAsItsFirstMessage()
    {
        await Action().RunAsync(Context(), Ct);

        var terminal = _host.Processes.ShouldHaveSingleItem();
        terminal.FileName.ShouldBe("open");
        terminal.Arguments.ShouldBe(["-a", "Terminal", File("start.sh")]);
        terminal.StandardInput.ShouldBeNull();
        (await System.IO.File.ReadAllTextAsync(File("start.sh"), Ct)).ShouldBe(
            $"""
            #!/bin/sh
            cd '{_code}' || exit 1
            unset CLAUDECODE
            exec '/usr/local/bin/claude' '--allowedTools' 'Read,Grep,Glob' '--add-dir' '{RunDirectory}' {InlinePrompt}

            """);
        if (!OperatingSystem.IsWindows())
            (System.IO.File.GetUnixFileMode(File("start.sh")) & UnixFileMode.UserExecute).ShouldBe(UnixFileMode.UserExecute);
    }

    [Test]
    public async Task Settings_SetTheCommandModelAndTools_AndNoToolsLeavesClaudeAsking()
    {
        await Action().RunAsync(Context(("command", "/opt/it's/claude"), ("model", "opus"), ("allowedTools", "")), Ct);

        (await System.IO.File.ReadAllLinesAsync(File("start.sh"), Ct))[3].ShouldBe(
            $"exec '/opt/it'\\''s/claude' '--model' 'opus' '--add-dir' '{RunDirectory}' {InlinePrompt}");
    }

    [Test]
    public async Task ALongPrompt_IsReadFromItsFile()
    {
        await Action().RunAsync(Context(("prompt", new string('x', ClaudeCommand.MaxInlinePrompt + 1))), Ct);

        (await System.IO.File.ReadAllLinesAsync(File("start.sh"), Ct))[3].ShouldEndWith($"'{ReadPrompt}'");
    }

    [Test]
    public async Task Windows_WritesACmdScript_AndOpensItInWindowsTerminalWhenInIt()
    {
        var windows = new ClaudePlatform(ClaudeOs.Windows, _ => null, _ => null);

        await Action(windows).RunAsync(Context(), Ct);
        await Action(windows with { Variable = name => name == "WT_SESSION" ? "1" : null }).RunAsync(Context(), Ct);

        (await System.IO.File.ReadAllTextAsync(File("start.cmd"), Ct)).ShouldBe(
            $"@echo off\r\ncd /d \"{_code}\"\r\nset CLAUDECODE=\r\n\"claude\" \"--allowedTools\" \"Read,Grep,Glob\" \"--add-dir\" \"{RunDirectory}\" \"{ReadPrompt}\"\r\n");
        _host.Processes.Select(p => (p.FileName, string.Join(' ', p.Arguments))).ShouldBe(
        [
            ("cmd.exe", $"/c start Claude Code /D {_code} cmd.exe /k {File("start.cmd")}"),
            ("wt.exe", $"-w 0 new-tab -d {_code} cmd.exe /k {File("start.cmd")}"),
        ]);
    }

    [Test]
    public void DefaultTerminal_IsATmuxWindowInTmux_ElseThePlatformsTerminal()
    {
        TerminalCommand.Default(Mac(("TMUX", "/tmp/tmux-501/default,1,0"))).ShouldBe(["tmux", "new-window", "-c", "{dir}", "{script}"]);
        TerminalCommand.Default(Mac()).ShouldBe(["open", "-a", "Terminal", "{script}"]);
        TerminalCommand.Default(new ClaudePlatform(ClaudeOs.Linux, _ => null, _ => null)).ShouldBe(["setsid", "-f", "x-terminal-emulator", "-e", "{script}"]);
    }

    [Test]
    public async Task TerminalSetting_ReplacesTheDefault_AppendingTheScriptWhenNotPlaced()
    {
        await Action().RunAsync(Context(("terminal", "ghostty --working-directory={dir} -e {script}")), Ct);
        await Action().RunAsync(Context(("terminal", "kitty --single-instance")), Ct);

        _host.Processes.Select(p => string.Join(" | ", [p.FileName, .. p.Arguments])).ShouldBe(
        [
            $"ghostty | --working-directory={_code} | -e | {File("start.sh")}",
            $"kitty | --single-instance | {File("start.sh")}",
        ]);
    }

    [Test]
    public async Task PromptSetting_ReplacesTheDefault_FillingFileAndMessagePlaceholders()
    {
        await Action().RunAsync(Context(("prompt", "Why is {Entity} #{sequenceNumber} ({deadLetterReason}) failing? See {messageFile} and {CONTEXTFILE}.\n{Message}")), Ct);

        (await System.IO.File.ReadAllTextAsync(File("prompt.md"), Ct)).ShouldBe(
            $"Why is orders DLQ #42 (MaxDeliveryCountExceeded) failing? See {File("message.json")} and {File("context.json")}.\n{MessageMarkdown.Format(Message).Trim()}");
    }

    [Test]
    public async Task WorkingDirectory_DefaultsToTheCurrentDirectory_AndMustExist()
    {
        await Action().RunAsync(Context(("workingDirectory", "")), Ct);
        _host.Processes.ShouldHaveSingleItem().WorkingDirectory.ShouldBe(Path.GetFullPath(Environment.CurrentDirectory));

        var missing = Path.Combine(_code, "missing");
        var error = await Should.ThrowAsync<InvalidOperationException>(() => Action().RunAsync(Context(("workingDirectory", missing)), Ct));
        error.Message.ShouldBe($"Working directory {missing} does not exist: azsb config set plugins.claude.workingDirectory <path to your code>");
    }

    [Test]
    public async Task TerminalFailing_ReportsItsOutput()
    {
        _host.Run = (_, _) => Task.FromResult(new ProcessResult(1, "", "Unable to find application named 'Terminal'\n"));

        var error = await Should.ThrowAsync<ProcessFailedException>(() => Action().RunAsync(Context(), Ct));

        error.Message.ShouldBe("open failed (exit code 1): Unable to find application named 'Terminal'");
    }

    [Test]
    public async Task TerminalNotFound_SaysHowToSetOne()
    {
        _host.Run = (_, _) => throw new InvalidOperationException("Could not start open", new Win32Exception(2));

        var error = await Should.ThrowAsync<InvalidOperationException>(() => Action().RunAsync(Context(), Ct));

        error.Message.ShouldBe("Could not open a terminal with open: azsb config set plugins.claude.terminal \"<command> {script}\"");
    }

    [Test]
    public void Plugin_RegistersTheAction()
    {
        var plugin = new ClaudePlugin();
        var services = new ServiceCollection();

        plugin.ConfigureServices(services);

        var action = services.ShouldHaveSingleItem().ImplementationInstance.ShouldBeOfType<AnalyseMessageAction>();
        action.PluginId.ShouldBe("claude");
        action.Icon.ShouldBe("🤖");
        action.CanRun(Context()).ShouldBeTrue();
    }

    [Test]
    public void Plugin_IsOnByDefault_WhenClaudeCodeIsFound()
    {
        var plugin = new ClaudePlugin();
        var environment = new PluginEnvironment(command => command == "claude", _ => null);

        plugin.Missing(Context().Settings, environment).ShouldBeNull();
        plugin.Missing(Context((ClaudeSettings.CommandKey, "~/bin/claude")).Settings, environment).ShouldBe("~/bin/claude not found");
    }
}
