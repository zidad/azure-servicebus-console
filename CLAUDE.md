# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Git workflow

Always fetch main and rebase before pushing a branch or opening a PR:

```bash
git fetch origin
git rebase origin/main
```

Resolve any conflicts, then push with `--force-with-lease`.

## Commands

```bash
dotnet build          # Build the project
dotnet run            # Run the console app
```

No tests exist in this project.

## Prerequisites

Requires an active Azure CLI login before running:
```bash
az login
```

Authentication uses `AzureCliCredential` — no connection strings or keys are used.

## Architecture

This is a .NET 10 terminal UI app that browses Azure Service Bus namespaces, queues, topics, and messages. The UI runs entirely in the terminal using **RazorConsole.Core** — a library that renders Blazor-style Razor components to the terminal via Spectre.Console.

**Key files:**
- `Program.cs` — Sets up the .NET Generic Host with `UseRazorConsole<App>()`, registers services, and configures file-based logging to `./logs/`.
- `Pages/` — One Razor component per screen, each with a `@page` route. Navigation uses Blazor's `NavigationManager`.
- `Components/App.razor` — Root router (`<Router AppAssembly="...">`).
- `NavigationState.cs` — Singleton that carries parameters between pages that can't be expressed as URL segments (selected message, topic/subscription name, return path).
- `Interfaces.cs` / `*Service.cs` — Service layer: `INamespaceService`, `IQueueService`, `ITopicService`, `IMessageService`.
- `FileCache.cs` — Filesystem cache; services stream cached data first, then live data.
- `FileLogger.cs` — Simple `ILoggerProvider` that writes structured logs to a timestamped file.

**UI pattern:** Use `/razorconsole` for a full component and pattern reference. Each page follows: `<Rows @onkeydown="HandleKeyDown">` root, header panel, content (table wrapped in `<Scrollable>`), action button bar. Navigation uses `Nav.NavigateTo("/route")`.

**Azure subscription IDs** are hardcoded in `Pages/ConnectionPage.razor` in the `SubscriptionOptions` array — update these to match the target environment.

**Message operations:**
- *Peek* uses `PeekLock` mode (non-destructive)
- *Receive* uses `ReceiveAndDelete` mode (destructive — permanently removes messages)
- *Requeue* receives from DLQ with PeekLock, resends to origin queue/topic, completes from DLQ
