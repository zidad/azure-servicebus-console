# azsb – Azure Service Bus CLI and terminal UI

[![Build](https://github.com/zidad/azure-servicebus-console/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/zidad/azure-servicebus-console/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Net.Azure.ServiceBusConsole?logo=nuget)](https://www.nuget.org/packages/Net.Azure.ServiceBusConsole)
[![NuGet downloads](https://img.shields.io/nuget/dt/Net.Azure.ServiceBusConsole?logo=nuget)](https://www.nuget.org/packages/Net.Azure.ServiceBusConsole)
[![Release](https://img.shields.io/github/v/release/zidad/azure-servicebus-console?logo=github)](https://github.com/zidad/azure-servicebus-console/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)
[![License: MIT](https://img.shields.io/github/license/zidad/azure-servicebus-console)](LICENSE)

`azsb` is a command-line tool for Azure Service Bus: browse namespaces, queues, topics and subscriptions, peek and receive messages, and resend or retry dead-letter messages, all from your terminal. A keyboard-driven alternative to the Azure portal's Service Bus Explorer and the standalone Service Bus Explorer app, on Linux, macOS and Windows.

![azsb browsing an Azure Service Bus queue's messages in the terminal](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/message-browser.png)

Built with [RazorConsole](https://github.com/RazorConsole/RazorConsole): Blazor Razor components rendered to the terminal via Spectre.Console.

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

The tool installs as a native executable on Linux, macOS and Windows (x64 and arm64); on other platforms it runs on the .NET 10 runtime. Without the .NET SDK, download the executable for your platform from the [releases](https://github.com/zidad/azure-servicebus-console/releases) (`SHA256SUMS` lists the checksums).

## Command line

```bash
azsb --subscription "Contoso Development" --namespace sb-orders-dev   # straight to the namespace's queues
azsb -s 00000000-0000-0000-0000-000000000000                         # connection page, subscription selected
azsb -s emulator -n localhost                                        # the local emulator
azsb -s "Contoso Development" -n sb-orders-dev -o orders/active      # straight to a queue's active messages
azsb --version                                                       # the installed version
azsb help                                                            # all flags
```

- `--subscription` / `-s`: Azure subscription by name or ID (case-insensitive); `emulator` or `Local Emulator` for the local emulator
- `--namespace` / `-n`: namespace in that subscription, by name or host name (`sb-orders-dev.servicebus.windows.net`); needs `--subscription`
- `--open` / `-o`: what to open in that namespace, by exact name (case-insensitive): a queue, a topic or `topic/subscription`; add `/active` or `/dlq` for the tab (dead letters by default). Or a list: `queues`, `topics`, `subscriptions`. Needs `--namespace`
- They apply at startup only: `Esc` back to the connection page works as usual. A subscription, namespace or `--open` target that isn't found is reported there
- `--theme` / `-t`: `dark` or `light` terminal background; `auto` (default) reads `COLORFGBG` where the terminal sets it, else dark; overrides the `ui.theme` setting. Colors are the terminal's 16 palette colors, so its color scheme sets their shade
- Without `--subscription`, the Azure CLI's current subscription (`az account set`) is preselected; the subscription list marks it `(az default)`
- `azsb help` lists the flags and commands; `azsb help <command>` (or `--help`) explains one

## Configuration

Settings live in a JSON file, read and written like `git config`:

```bash
azsb config set ui.theme light   # true/false and integers are stored as such, the rest as text
azsb config get ui.theme         # prints the value; exit code 1 when unset
azsb config unset ui.theme       # also removes sections it leaves empty
azsb config list                 # key=value lines; --all adds unset known keys with default and description
azsb config path                 # the file's location
```

- File: `$AZSB_CONFIG` if set, else `$XDG_CONFIG_HOME/azsb/config.json`, else `~/.config/azsb/config.json` (Windows: `%APPDATA%\azsb\config.json`)
- Dotted keys are nested objects (`{"ui": {"theme": "light"}}`); keys ignore case. Hand edits are kept: every command reads the file first
- Environment variables override the file: `AZSB_` plus the key with `__` for dots (`AZSB_UI__THEME=dark`); a command-line flag overrides both
- `set` warns about unknown keys but sets them; keys under `plugins.` are never unknown

| Key | Default | Description |
|---|---|---|
| `ui.theme` | `auto` | Colors for a dark or light terminal background: `auto` (`COLORFGBG`, else dark), `dark` or `light`; `--theme` overrides it |

`azsb config list --all` also shows the plugins' keys; `list` masks secret ones (tokens).

## Plugins

Plugins add actions on a message: press `F2` (or click `[F2] ⚡ Actions…`) in the message browser or on the message detail page, pick one with `↑`/`↓` and `Enter`. While it runs it shows its progress (`Esc` cancels); when done, its result with a link or file you can open (`O`). Failures show as errors. Each outcome is recorded in the session's activity.

They ship with the tool. Each is on when what it needs is found, e.g. its command-line tool on the `PATH`; `enabled` overrides that:

```bash
azsb plugins                               # each plugin: id, name, enabled or disabled (and why), its settings and defaults
azsb config set plugins.<id>.enabled false # turn one off; it offers no actions then
azsb config set plugins.<id>.enabled true  # turn one on, found or not
azsb config unset plugins.<id>.enabled     # back to auto
azsb config set plugins.<id>.<key> <value> # a plugin setting, e.g. a repository or a token
```

- Settings live under `plugins.<id>.` in the [config file](#configuration), or in `AZSB_PLUGINS__<ID>__<KEY>` environment variables
- Secret settings are masked by `azsb plugins` and `azsb config list`
- Files plugins write (message exports and the like) go to the state directory: `$XDG_STATE_HOME/azsb`, else `~/.local/state/azsb` (Windows: `%LOCALAPPDATA%\azsb\state`)

### Editor (`editor`)

📝 *Open in editor* writes the message to a JSON file (properties, and the body as embedded JSON when it is JSON, else as text) and opens it in your editor. On by default when the editor command is found. Reopening a message overwrites its file, `<entity>-<sequence number>.json` in the state directory's `messages` folder.

| Setting | Default | |
|---|---|---|
| `plugins.editor.command` | `code` | GUI editor executable on your `PATH`, e.g. `idea`, `subl`; on Windows `code.cmd`. Terminal editors (`vim`) can't share azsb's screen |
| `plugins.editor.arguments` | the file | Arguments, split on spaces; `{file}` is the message file, appended when absent, e.g. `--new-window {file}` |
| `plugins.editor.directory` | `messages` folder | Folder for the message files |

```bash
azsb config set plugins.editor.command idea
```

### GitHub (`github`)

**🐙 Create GitHub issue** files the message as an issue with the [GitHub CLI](https://cli.github.com), signed in as your `gh auth login`. The body has the message (fields, application properties, body) and, unless turned off, where it came from and the session's recent activity; bodies over GitHub's 65,536 characters are truncated with a note. The result links to the new issue. On by default when `gh` is found.

```bash
azsb config set plugins.github.repo owner/name
azsb config set plugins.github.labels bug,dead-letter
```

| Setting | Default | |
|---|---|---|
| `plugins.github.repo` | | Repository to create issues in, `owner/name` (required) |
| `plugins.github.labels` | | Labels, comma-separated |
| `plugins.github.assignees` | | Assignees, comma-separated; `@me` is you |
| `plugins.github.titleTemplate` | `{entity}: {subject}` | Placeholders `{entity}`, `{subject}`, `{messageId}`, `{sequenceNumber}`, `{namespace}`, `{deadLetterReason}`; a message without a subject uses its id |
| `plugins.github.cli` | `gh` | The GitHub CLI, on the `PATH` or a full path |
| `plugins.github.includeContext` | `true` | Add the session context and recent activity |

### Linear (`linear`)

`📋 Create Linear issue` files the message as an issue in a Linear team: the title from a template, the description in Markdown with the message's fields, application properties and body, and (unless turned off) where it came from and the session's recent activity. The result links to the issue. On by default when an API key is set.

Create a personal API key in Linear under *Settings › Security & access › Personal API keys*, then:

```bash
azsb config set plugins.linear.apiKey lin_api_...   # or export LINEAR_API_KEY
azsb config set plugins.linear.team ENG
```

| Setting | Default | |
|---|---|---|
| `plugins.linear.apiKey` | `LINEAR_API_KEY` | Personal API key; secret |
| `plugins.linear.team` | | Key of the team, e.g. `ENG`; required |
| `plugins.linear.labels` | | Comma-separated label names of the team or workspace, e.g. `bug,servicebus`; an unknown one is an error |
| `plugins.linear.titleTemplate` | `{entity}: {subject}` | Placeholders `{entity}`, `{subject}` (`Message #42` when empty), `{messageId}`, `{sequenceNumber}`, `{namespace}`, `{deadLetterReason}` |
| `plugins.linear.includeContext` | `true` | Adds the session context to the description |

### Claude Code (`claude`)

`🤖 Analyse with Claude Code` writes the message (`message.json`), the session context (`context.json`: where you are, what you did recently) and a prompt (`prompt.md`) to `plugins/claude/<entity>-<seq>-<time>/` in the state directory, then opens a new terminal window with a full, interactive [Claude Code](https://docs.claude.com/en/docs/claude-code) session in your codebase. Its first message is that prompt, with the message and the session context in it: find the producer and consumer of the entity and, for a dead-lettered message, explain the likely cause and suggest fixes. You carry on the conversation there; azsb doesn't wait for it. It uses your own Claude Code login. On by default when `claude` is found.

The window is a new tmux window when azsb runs inside tmux; otherwise Terminal on macOS, a Windows Terminal tab (or a `cmd` window) on Windows, `x-terminal-emulator` on Linux. Set `terminal` for another one.

```bash
azsb config set plugins.claude.workingDirectory ~/code/orders-service
azsb config set plugins.claude.terminal "open -a iTerm {script}"
```

| Setting | Default | |
|---|---|---|
| `command` | `claude` | Claude Code executable |
| `workingDirectory` | where azsb was started | The codebase that sends or handles the messages |
| `allowedTools` | `Read,Grep,Glob` | Tools Claude may use without asking (`--allowedTools`); empty: it asks for each |
| `model` | Claude Code's | `--model`, e.g. `sonnet`, `opus` |
| `prompt` | built in | First message; `{message}` and `{context}` insert them as Markdown; also `{messageFile}`, `{contextFile}`, `{entity}`, `{subject}`, `{sequenceNumber}`, `{messageId}`, `{namespace}`, `{deadLetterReason}` |
| `terminal` | per platform | Command that opens a window running `{script}` (the run's `start.sh`/`start.cmd`), split on spaces; `{dir}` is the working directory; `{script}` is appended when absent |

### Trace (`trace`)

`🔍 Open trace` opens the message's distributed trace in your browser. It is offered for messages with a W3C trace context in their application properties: `traceparent`, `Diagnostic-Id` (Azure SDK), `MT-Activity-Id` (MassTransit), `parent-id` (Wolverine), else any property whose value is one (`00-<trace id>-<span id>-<flags>`). By default it opens the end-to-end transaction in Application Insights in the Azure portal, on the request that handled the message (found with `az monitor app-insights query`, from an hour before the message was enqueued to a day after). When `az` can't query it or finds nothing, it opens the Application Insights logs of the trace (`operation_Id`) instead. When `appInsights` is unset, it takes the only Application Insights of the namespace's subscription, found with `az`. Set `url` for another trace viewer (Jaeger, Grafana, Datadog, ...). On by default when `az` is found, or when `appInsights` holds a resource id or `url` is set.

```bash
azsb config set plugins.trace.appInsights ai-orders     # a name in the namespace's subscription, or a resource id
azsb config set plugins.trace.url "http://localhost:16686/trace/{traceId}"
```

| Setting | Default | |
|---|---|---|
| `appInsights` | the only one in the subscription | Application Insights resource id (`/subscriptions/.../components/<name>`), or its name in the namespace's subscription |
| `tenant` | your default | Tenant id or domain for the portal link |
| `url` | Azure portal | URL template: `{traceId}`, `{spanId}`, `{start}`/`{end}` (ISO 8601), `{startMs}`/`{endMs}` (Unix ms), and `{entity}`, `{subject}`, `{messageId}`, `{sequenceNumber}`, `{namespace}`; values are URL-escaped |
| `cli` | `az` | The Azure CLI, to find Application Insights |

## Features

- Lists the Azure subscriptions of your `az` login by name, hides the ones without Service Bus namespaces, and lets you filter them; the local emulator is always available
- Starts in the Azure CLI's current subscription, or in the subscription and namespace given on the [command line](#command-line)
- Namespace details (resource group, location, SKU, status) on the connection page
- Switch subscription, namespace or section (Queues, Topics, All Subscriptions) from dropdowns in the header
- Offers what the namespace supports: Basic tier namespaces have no topics, so their Topics and All Subscriptions sections are hidden; the local emulator can't delete entities, so its lists have no Delete
- Queues, topics and subscriptions with live message counts (active, DLQ, scheduled, transfer): everything loads from a local cache first, then polls every 15 seconds; changed counts flash briefly. When the live listing fails, the cached rows stay
- Sort any list by clicking a column header (ascending, descending, off); the default sort shows in the header (DLQ for queues and all subscriptions, name for a topic's subscriptions)
- Filter queues, topics, subscriptions and Azure subscriptions by typing
- Color-coded counts and states: dead letters in red, zero counts dimmed, lock and browse modes highlighted
- Peek, receive (PeekLock) or receive-and-delete a chosen number of messages, like the Azure portal's Service Bus Explorer
- Multi-select messages (`Space`, click the checkbox, `A` all, `I` invert) and act on all of them at once
- Locked messages: complete, abandon, dead-letter or requeue; locks renew automatically and are released when you leave
- Split message browser: the list on top, a preview of the message under the cursor below (properties left, JSON-highlighted body right); `Enter` opens it full screen
- Dead-letter queue browsing per queue and per subscription; requeue dead-lettered messages to their origin
- Delete queues, topics or subscriptions (Azure namespaces); purge (dead-letter) queues
- Metrics panel: Azure Monitor charts of throughput and backlog for the selected namespace, queue or topic, over 1 hour to 30 days; see [Metrics](#metrics)
- A namespace firewall that blocks your IP is explained in one line instead of a raw 401 response
- The connection page shows the cache's path and a button to clear it
- Mouse support: buttons, rows, column headers, header dropdowns, wheel scrolling

## Screenshots

*Sample data from the test suite; regenerate with `tools/update-screenshots.sh`.*

**Connection**: Azure subscriptions of your login, filter, namespace details, the selected namespace's metrics

![Connection screen](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/connection-metrics.png)

**Queues**: color-coded counts, sortable columns, type to filter, metrics of the selected queue

![Queue list](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/queues.png)

**Metrics over a week**: zoomed out to 7 days with `-`

![Queue list with 7 days of metrics](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/queues-7d.png)

**Live counts**: lists poll every 15 seconds; a changed count flashes and fades

![Queue list with a changed count](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/queues-live.png)

**Header dropdowns**: switch namespace (or subscription, or section) in place

![Namespace dropdown](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/namespace-dropdown.png)

![Section dropdown](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/section-dropdown.png)

**Message browser**: dead-letter reasons, delivery counts, multi-select, preview of the message under the cursor

![Message browser](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/message-browser.png)

**Retry**: confirm the flow, follow its progress, check the result

![Retry confirm](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/retry-confirm.png)

![Retry progress](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/retry-progress.png)

![Retry result](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/retry-result.png)

**Message detail**: properties and highlighted JSON body

![Message detail](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/message-detail.png)

**All subscriptions** across every topic

![All subscriptions](https://raw.githubusercontent.com/zidad/azure-servicebus-console/main/docs/screenshots/all-subscriptions.png)

## Keyboard shortcuts

### All screens

| Key | Action |
|-----|--------|
| `Tab` / `Shift+Tab` | Move focus between controls |
| `Enter` / `Space` | Activate the focused button |
| `PgUp` / `PgDn` / `Home` / `End` | Page through the focused list |
| `Y` / `Enter`, `N` / `Esc` | Confirm or cancel a prompt |
| `Esc` | Go back to previous screen |
| Click | Activate a button, or select a row; click the selected row to open it |
| Click a column header | Sort by it: ascending, descending, off |
| Click subscription / namespace / section in the header | Open a dropdown to switch |
| Mouse wheel | Scroll the list under the pointer |

### Connection screen

| Key | Action |
|-----|--------|
| `↑` / `↓` | Choose a subscription or namespace |
| Type / `Backspace` | Filter Azure subscriptions by name or ID (in the subscription list) |
| `Enter` | Subscriptions: go to namespaces; namespaces: open queues |
| `F1` | Open the queue list |
| `F2` | Open the topic list *(not on Basic tier)* |
| `F3` | Open the all-subscriptions list *(not on Basic tier)* |

### Queue list / Topic list / Subscription list

| Key | Action |
|-----|--------|
| Type | Filter list by name |
| `Backspace` | Remove last filter character |
| `↑` / `↓` | Scroll list |
| `Enter` | Open selected item |
| `F5` | Refresh (also the metrics charts) |
| `F9` / `Del` | Delete focused item *(not on the local emulator)* |
| `F11` | Purge the focused queue's or subscription's dead-letter queue |

### Metrics panel

Connection screen and lists, Azure namespaces only.

| Key | Action |
|-----|--------|
| `F12` | Show or hide the panel |
| `+` or `=` | Zoom in: shorter time range |
| `-` or `_` | Zoom out: longer time range; only while the filter is empty, since names can contain `-` and `_` |
| `[-]` / `[+]` in the panel header | The same, by click or `Tab` |

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
| `F2` | Plugin actions on the message under the cursor *(when a plugin offers one)* |

Actions apply to the selected messages, or to the focused one when nothing is selected.

### Message detail

| Key | Action |
|-----|--------|
| `↑` / `↓` | Scroll message body |
| `F9` `F4` `F3` `F10` | Same actions as the browser, for this message |
| `F2` | Plugin actions on this message *(when a plugin offers one)* |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) logged in:

```bash
az login
```

Authentication uses `AzureCliCredential` — no connection strings or API keys are stored. The subscriptions you see are the ones your `az login` account can access.

- The metrics panel reads `Microsoft.Insights/metrics/read` on the namespace: Reader and Monitoring Reader include it, the Azure Service Bus Data roles don't. Without it the panel shows the error in one line
- A namespace firewall must allow your IP. When it doesn't, the app says so: connect through an allowed network (VPN), or add your IP under Networking for the namespace in the Azure portal

## Running from source

The .NET 10 SDK builds and runs the app, also from a folder whose `global.json` pins SDK 10. With the [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0) (`global.json`), RazorConsole also builds for `net11.0`.

```bash
git clone --recurse-submodules https://github.com/zidad/azure-servicebus-console
cd azure-servicebus-console
az login
dotnet run --project src/ServiceBusConsole
dotnet run --project src/ServiceBusConsole -- -s emulator   # command-line flags go after --
```

Logs are written to `logs/session-<timestamp>.log` next to the app's binaries (`src/ServiceBusConsole/bin/Debug/net10.0/logs/` when run from source).

### Local emulator

```bash
docker compose up -d
```

Starts the [Service Bus emulator](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator) on `localhost:5672` with the queues and topics of `emulator/Config.json`, and seeds them with sample messages. The emulator has no management API: the app reads its entities from the nearest `emulator/Config.json`, looking up from the current directory, then from the app's own folder (so `dotnet run --project` works from anywhere), and counts come from peeking (up to 1,000 messages). Without a management API it can't delete entities either, so Delete is hidden there.

## How message operations work

The browser follows the Service Bus Explorer in the Azure portal:

- **Peek** is read-only.
- **Retry** (dead-letter queue): peek, select the messages to retry, press `F10`. The selected messages are locked, sent to the origin queue (or the topic, for subscriptions: every subscription gets a copy) in order, then removed from the dead-letter queue. Service Bus can only lock from the front of a queue, so dead-lettered messages ahead of them are locked briefly and released; their delivery count goes up by one and they stay in the dead-letter queue. Already locked or received messages are retried the same way, without the extra lock step. On Standard and Premium namespaces each chunk of up to 100 messages moves in one transaction: the copies are sent and the originals completed together, or not at all, so a failure (or `Esc`) leaves the chunk in the dead-letter queue instead of duplicating it. Basic tier namespaces and the local emulator have no transactions: there a failure between sending and completing can leave a copy in the queue while the original stays dead-lettered.
- **Receive (PeekLock)** locks the messages on a receiver that stays open. Locks are renewed in the background and abandoned when you leave the page or load something else, so messages come back right away instead of after lock expiry.
- **Receive (ReceiveAndDelete)** removes messages immediately; requeue them before leaving or they are gone.
- **Resend** (active queue, peeked) sends copies; the originals stay.

Bulk actions show a progress dialog (`Esc` cancels). When done it shows the result, including any failures, until you press `Enter` (OK); failed messages stay selected so you can retry. If nothing is left in the list, OK returns to the queue or subscription list.

## Metrics

The metrics panel charts the current namespace's Azure Monitor metrics: incoming and outgoing messages per interval on the left, the active and dead-lettered backlog on the right.

- **Lists** (Queues, Topics, Subscriptions, All subscriptions): at the bottom, for the selected queue or topic. A subscription shows its topic's metrics: Azure Monitor has no per-subscription dimension
- **Connection page**: at the bottom of the screen, below the subscription and namespace lists, for the selected namespace, in a fixed-height area so nothing moves when the selection changes. It needs room for the logo, every subscription row and three namespace rows (about 42 lines and up)
- **Time range**: `[-] last 24 h [+]` in the panel header steps through 1 h, 3 h, 6 h, 12 h, 1 d, 3 d, 7 d, 14 d and 30 d (default 1 d) and is remembered. The interval (1 minute to 1 day) follows the range and the chart width; the time axis is in minutes, hours or days
- Cached charts show first, then the live ones; `F5` on a list reloads them
- Shown by default on terminals of 35+ lines; `F12` shows or hides it, remembered across runs. Always hidden on terminals too short for it
- Azure namespaces only: the local emulator has no Azure Monitor metrics, so its lists have no panel and the connection page shows a one-line note in its place

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

- [Azure.Messaging.ServiceBus](https://www.nuget.org/packages/Azure.Messaging.ServiceBus) — Service Bus client
- [Azure.ResourceManager.ServiceBus](https://www.nuget.org/packages/Azure.ResourceManager.ServiceBus) — namespace discovery via ARM
- [Azure.ResourceManager.Monitor](https://www.nuget.org/packages/Azure.ResourceManager.Monitor) — Azure Monitor metrics
- [System.CommandLine](https://www.nuget.org/packages/System.CommandLine) — command line
