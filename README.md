# azure-servicebus-console

A terminal UI browser for Azure Service Bus — explore namespaces, queues, topics, and messages directly from your terminal.

## Install

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) and an active Azure CLI login.

```bash
dotnet tool install -g Net.Azure.ServiceBusConsole
az login
azsb
```

To update to the latest version:

```bash
dotnet tool update -g Net.Azure.ServiceBusConsole
```

## Features

- Lists the Azure subscriptions of your `az` login by name, hides the ones without Service Bus namespaces, and lets you filter them; the local emulator is always available
- Namespace details (resource group, location, SKU, status) on the connection page
- Switch subscription, namespace or section (Queues, Topics, All Subscriptions) from dropdowns in the header
- Queues, topics and subscriptions with live message counts (active, DLQ, scheduled, transfer): everything loads from a local cache first, then polls every 15 seconds; changed counts flash briefly
- Sort any list by clicking a column header (ascending, descending, off); the default sort (DLQ) shows in the header
- Filter queues, topics, subscriptions and Azure subscriptions by typing
- Color-coded counts and states: dead letters in red, zero counts dimmed, lock and browse modes highlighted
- Peek, receive (PeekLock) or receive-and-delete a chosen number of messages, like the Azure portal's Service Bus Explorer
- Multi-select messages (`Space`, click the checkbox, `A` all, `I` invert) and act on all of them at once
- Locked messages: complete, abandon, dead-letter or requeue; locks renew automatically and are released when you leave
- Split message browser: the list on top, a preview of the message under the cursor below (properties left, JSON-highlighted body right); `Enter` opens it full screen
- Dead-letter queue browsing per queue and per subscription; requeue dead-lettered messages to their origin
- Delete queues, topics or subscriptions; purge (dead-letter) queues
- Mouse support: buttons, rows, column headers, header dropdowns, wheel scrolling

## Screenshots

*Sample data from the test suite; regenerate with `tools/update-screenshots.sh`.*

**Connection**: Azure subscriptions of your login, filter, namespace details

![Connection screen](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/connection.png)

**Queues**: color-coded counts, sortable columns, type to filter

![Queue list](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/queues.png)

**Header dropdowns**: switch namespace (or subscription, or section) in place

![Namespace dropdown](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/namespace-dropdown.png)

![Section dropdown](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/section-dropdown.png)

**Message browser**: dead-letter reasons, delivery counts, multi-select

![Message browser](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/message-browser.png)

**Message detail**: properties and highlighted JSON body

![Message detail](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/message-detail.png)

**All subscriptions** across every topic

![All subscriptions](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/all-subscriptions.png)

## Keyboard shortcuts

### All screens

| Key | Action |
|-----|--------|
| `Tab` / `Shift+Tab` | Move focus between controls |
| `Esc` | Go back to previous screen |
| Click | Activate a button, or select a row; click the selected row to open it |
| Click a column header | Sort by it: ascending, descending, off |
| Click subscription / namespace / section in the header | Open a dropdown to switch |
| Mouse wheel | Scroll the list under the pointer |

### Connection screen

| Key | Action |
|-----|--------|
| `↑` / `↓` | Choose a subscription or namespace |
| Type / `Backspace` | Filter Azure subscriptions (in the subscription list) |
| `Enter` | Subscriptions: go to namespaces; namespaces: open queues |
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
| `F7` | Peek… (count, optional start sequence number) |
| `F8` | Receive… (count, PeekLock or ReceiveAndDelete) |
| `Space` | Select / deselect message (or click its checkbox) |
| `A` / `I` | Select all / invert selection |
| `Esc` | Clear selection, then back |
| `F9` / `Del` | Complete *(locked)* |
| `F4` | Abandon *(locked)* |
| `F3` | Dead-letter *(locked, active queue)* |
| `F10` | **Retry** *(dead-letter queue)* / Requeue *(received-and-deleted)* / Resend copy *(peeked, active queue)* |
| `F11` | Purge |
| `↑` / `↓` | Move through message list |
| `Tab` | Move into the preview body (`↑` / `↓` scroll it; page keys keep working) |
| `Enter` | Open the message full screen |

Actions apply to the selected messages, or to the focused one when nothing is selected.

### Message detail

| Key | Action |
|-----|--------|
| `↑` / `↓` | Scroll message body |
| `F9` `F4` `F3` `F10` | Same actions as the browser, for this message |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) logged in:

```bash
az login
```

Authentication uses `AzureCliCredential` — no connection strings or API keys are stored. The subscriptions you see are the ones your `az login` account can access.

## Running from source

```bash
git clone --recurse-submodules https://github.com/zidad/azure-servicebus-console
cd azure-servicebus-console
az login
dotnet run
```

Logs are written to `./logs/session-<timestamp>.log` on each run.

## How message operations work

The browser follows the Service Bus Explorer in the Azure portal:

- **Peek** is read-only.
- **Retry** (dead-letter queue): peek, select the messages to retry, press `F10`. The selected messages are locked, sent to the origin queue (or the topic, for subscriptions: every subscription gets a copy) in order, then removed from the dead-letter queue. Service Bus can only lock from the front of a queue, so dead-lettered messages ahead of them are locked briefly and released; their delivery count goes up by one and they stay in the dead-letter queue. Already locked or received messages are retried the same way, without the extra lock step.
- **Receive (PeekLock)** locks the messages on a receiver that stays open. Locks are renewed in the background and abandoned when you leave the page or load something else, so messages come back right away instead of after lock expiry.
- **Receive (ReceiveAndDelete)** removes messages immediately; requeue them before leaving or they are gone.
- **Resend** (active queue, peeked) sends copies; the originals stay.

Bulk actions show a progress dialog (`Esc` cancels). When done it shows the result, including any failures, until you press `Enter` (OK); failed messages stay selected so you can retry. If nothing is left in the list, OK returns to the queue or subscription list.

## Releasing

Push a version tag on a commit on `main`:

```bash
git tag v1.2.3 origin/main
git push origin v1.2.3
```

The [publish workflow](.github/workflows/publish.yml) runs the tests, packs the tool with the tag's version, pushes it to NuGet.org and creates a GitHub release with generated notes and the package attached. There is no version number in the code.

Publishing uses [NuGet trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) — no API key secret is stored. The workflow exchanges a GitHub OIDC token for a short-lived nuget.org API key via `NuGet/login`.

This requires a trusted publishing policy on nuget.org (Account → Trusted Publishing) with:

| Field | Value |
| --- | --- |
| Repository Owner | `zidad` |
| Repository | `azure-servicebus-console` |
| Workflow File | `publish.yml` |
| Environment | *(leave empty)* |

## Built with

- [RazorConsole](https://github.com/RazorConsole/RazorConsole) — Blazor Razor components rendered to the terminal via Spectre.Console
- [Azure.Messaging.ServiceBus](https://www.nuget.org/packages/Azure.Messaging.ServiceBus) — Service Bus client
- [Azure.ResourceManager.ServiceBus](https://www.nuget.org/packages/Azure.ResourceManager.ServiceBus) — namespace discovery via ARM
