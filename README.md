# azure-servicebus-console

A terminal UI browser for Azure Service Bus — explore namespaces, queues, topics, and messages directly from your terminal.

## Install

Requires .NET 10 SDK and an active Azure CLI login.

```bash
dotnet tool install -g Net.Azure.ServiceBusConsole
az login
sbconsole
```

> **Note:** Subscription IDs are currently hardcoded. See [Configuration](#configuration).

## Features

- Browse Service Bus namespaces across multiple Azure subscriptions
- Queues and topics with live message counts (active, DLQ, scheduled, transfer) — animated spinner while refreshing from cache
- Filter queues, topics, and subscriptions by typing in the filter box
- Scrollable tables — arrow keys or scrollbar to navigate long lists
- Peek messages non-destructively (PeekLock) or receive/delete them (ReceiveAndDelete)
- Inspect message properties and body (auto pretty-printed if JSON) with a scrollable body panel
- Dead-letter queue browsing per queue and per subscription
- **Requeue DLQ messages** — fetch-lock a dead-lettered message, republish it to the origin queue/topic, and complete it in one step
- Delete individual queues, topics, subscriptions, or messages

## Screenshots

<!-- screenshots go here -->

## Keyboard shortcuts

### All screens

| Key | Action |
|-----|--------|
| `Tab` / `Shift+Tab` | Move focus between controls |
| `Esc` | Go back to previous screen |

### Connection screen

| Key | Action |
|-----|--------|
| `F1` | Open queue browser |
| `F2` | Open topic browser |
| `F3` | Open all-subscriptions view |

### Queue list / Topic list / Subscription list

| Key | Action |
|-----|--------|
| Type | Filter list by name |
| `Backspace` | Remove last filter character |
| `↑` / `↓` | Scroll list |
| `Enter` | Open selected item |
| `F5` | Refresh |
| `F9` / `Del` | Delete focused item |

### Queue browser / Subscription browser

| Key | Action |
|-----|--------|
| `F6` | Toggle between active queue and DLQ |
| `F7` | Peek messages (non-destructive) |
| `F8` | Receive messages (destructive — deletes from queue) |
| `F9` / `Del` | Delete focused message |
| `F10` | Requeue focused DLQ message *(DLQ mode only)* |
| `↑` / `↓` | Scroll message list |
| `Enter` | View message detail |

### Message detail

| Key | Action |
|-----|--------|
| `↑` / `↓` | Scroll message body |
| `F9` / `Del` | Delete message |
| `F10` | Requeue message *(DLQ messages only)* |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) logged in:

```bash
az login
```

Authentication uses `AzureCliCredential` — no connection strings or API keys are stored.

## Running from source

```bash
git clone --recurse-submodules https://github.com/zidad/azure-servicebus-console
cd azure-servicebus-console
az login
dotnet run
```

Logs are written to `./logs/session-<timestamp>.log` on each run.

## Configuration

Azure subscription IDs are hardcoded in `Pages/ConnectionPage.razor` in the `SubscriptionOptions` array. Update these to match your environment before running from source.

## How requeue works

When a message ends up in a dead-letter queue, `F10` on the message list or message detail will:

1. Receive the message from the DLQ with PeekLock
2. Send an identical copy to the origin entity (queue or topic)
3. Complete (permanently remove) the original from the DLQ

The message is immediately removed from the list on success.

## Built with

- [RazorConsole](https://github.com/RazorConsole/RazorConsole) — Blazor Razor components rendered to the terminal via Spectre.Console
- [Azure.Messaging.ServiceBus](https://www.nuget.org/packages/Azure.Messaging.ServiceBus) — Service Bus client
- [Azure.ResourceManager.ServiceBus](https://www.nuget.org/packages/Azure.ResourceManager.ServiceBus) — namespace discovery via ARM
