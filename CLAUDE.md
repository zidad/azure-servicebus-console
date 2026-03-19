# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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

This is a .NET 9 terminal UI app that browses Azure Service Bus namespaces, queues, topics, and messages. The UI runs entirely in the terminal using **RazorConsole.Core** — a library that renders Blazor-style Razor components to the terminal via Spectre.Console.

**Key files:**
- `Program.cs` — Sets up the .NET Generic Host with `UseRazorConsole<App>()`, registers `AzureCliCredential`, `ArmClient`, and `SBClient` as singletons, and configures file-based logging to `./logs/`.
- `IServiceBusClient.cs` — `SBClient` class: all Azure Service Bus data access. Uses `ArmClient` (Azure Resource Manager) to discover namespaces across subscriptions, and `ServiceBusAdministrationClient` / `ServiceBusClient` for queue/topic/message operations.
- `Components/App.razor` — The single Razor component containing all UI. Uses a `Screen` enum state machine (`Connection → QueueList/TopicList → QueueBrowser → MessageDetail`) and calls `StateHasChanged()` to trigger re-renders.
- `FileLogger.cs` — A simple `ILoggerProvider` that writes structured logs to a timestamped file.

**UI pattern:** All screens live in `App.razor` as `@if (_screen == Screen.X)` blocks. Navigation is managed by setting `_screen` and calling `StateHasChanged()`. RazorConsole components (`<Select>`, `<TextButton>`, `<TextInput>`, `<SpectreTable>`, etc.) map to Spectre.Console primitives.

**Azure subscription IDs** are hardcoded in the `Subscriptions` array at the top of `App.razor`'s `@code` block — update these to match the target environment.

**Message operations:**
- *Peek* uses `PeekLock` mode (non-destructive)
- *Receive* uses `ReceiveAndDelete` mode (destructive — permanently removes messages)
