# azure-servicebus-console

A terminal UI browser for Azure Service Bus — explore namespaces, queues, topics, and messages directly from your terminal.

## Features

- Browse Service Bus namespaces across Azure subscriptions
- View queues and topics with live message counts (active, DLQ, scheduled, transfer)
- Filter queues and topics by name
- Peek messages non-destructively or receive (delete) them
- Inspect message body (pretty-printed if JSON) and application properties
- Dead-letter queue browsing

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Azure CLI logged in with access to the target subscriptions:

```bash
az login
```

## Running

```bash
dotnet run
```

Logs are written to `./logs/session-<timestamp>.log` on each run.

## Configuration

Azure subscription IDs are hardcoded in `Components/App.razor` in the `Subscriptions` array. Update these to match your environment.

## Built with

- [RazorConsole](https://github.com/RazorConsole/RazorConsole) — Razor + Spectre.Console terminal UI framework
- [Azure.Messaging.ServiceBus](https://www.nuget.org/packages/Azure.Messaging.ServiceBus) — Service Bus client
- [Azure.ResourceManager.ServiceBus](https://www.nuget.org/packages/Azure.ResourceManager.ServiceBus) — namespace discovery via ARM
