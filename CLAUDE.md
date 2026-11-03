# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Git workflow

Always fetch main and rebase before pushing a branch or opening a PR:

```bash
git fetch origin
git rebase origin/main
```

Resolve any conflicts, then push with `--force-with-lease`.

The fork's `azsb` branch (`lib/RazorConsole`) is always **one commit** on top of upstream `RazorConsole/RazorConsole` main, so `git diff upstream/main azsb` shows every customization at once. Keep that commit minimal, aiming to switch back to the RazorConsole NuGet package:

- Component behaviour lives in the app (`Components/`, `Components/Lists/`): `SelectableList`, `NavigableTBody`, `SelectableRow` and `KeyButton` render RazorConsole's element protocol (`<scrollable>`, `<tbody>`, `<tr data-selected-bg>`, `data-focusable`) themselves
- The fork only holds engine fixes and the smallest hooks the app cannot add itself, each with an upstream PR or one to open; drop it once upstream has it

With every change:

- Rebase that commit onto the latest upstream main; where upstream merged one of our PRs or an equivalent fix, keep upstream's version
- Check each remaining customization for an upstream replacement; remove the patch and the app workaround when one exists
- New fork changes: commit, then fold back into one commit (`git reset --soft upstream/main && git commit`); the message lists the customizations by area
- Upstream PRs come from separate `fix/*` branches off upstream main, never from `azsb`
- Run the fork and app tests, push `azsb` with `--force-with-lease`, bump the submodule pointer in the same PR

## Releases

Release by pushing a version tag on a `main` commit: `git tag v1.2.3 origin/main && git push origin v1.2.3`. `publish.yml` then tests, packs with the tag's version, pushes to NuGet and creates a GitHub release with generated notes. There is no version in the code or in PR titles.

## Commands

```bash
dotnet build ServiceBusConsole.slnx        # Build everything
dotnet run --project src/ServiceBusConsole # Run the console app
dotnet test --solution ServiceBusConsole.slnx --max-parallel-test-modules 1  # All tests; one project at a time keeps the timing-sensitive UI tests stable
```

## Tests

Tests use TUnit on Microsoft.Testing.Platform with Shouldly assertions; `Ct` (`tests/TestRun.cs`) is the running test's cancellation token. CI writes the TRX results to the job summary (failed tests listed) and uploads TUnit's HTML reports as artifacts.

`tests/ServiceBusConsole.Tests` mounts the real `App` in RazorConsole.Testing's `HeadlessTerminal` with fake services (`Fakes.cs`); no Azure or emulator needed. `AppHarness` opens fake namespaces like the real opener (`FakeEmulatorNamespace`, `FakeBasicNamespace` for SKU Basic, `FakeStandardNamespace` otherwise) over shared fake services; set `AppHarness.OpenNamespace` for another mix of features. `tests/ServiceBusConsole.AzureServiceBus.Tests` covers the Azure project's internals (`LockedMessages` on a fake receiver, message mapping, emulator config, the opener's tiers and features).
- Start through `AppHarness.OpenQueuesAsync()` (clicks through the connection page). Navigating `NavigationManager` from the test thread does not render.
- Pages focus their table after rendering; call `AppHarness.WaitForTableFocusAsync` before sending keys to a new page, and after a dialog closes.
- Dialogs take focus one render after their text appears: `AppHarness.WaitForFocusAsync(terminal, focusOrder)` before typing into one.
- RazorConsole bugs found by tests are fixed in the fork (`azsb`), not worked around in the app.
- Keys go only to the focused element; they do not bubble. Page `HandleKeyDown` runs only via components that forward `OnKeyDown`.
- `AppHarness` gives each test its own `FileCache` directory under the temp folder.
- UI tests run at most one per core (`TestParallelism.cs`), and one at a time per class: a new UI test class gets `[NotInParallel(nameof(TheClass))]`. TUnit's defaults (4 per core, tests of a class in parallel) starve their render timeouts.
- Screenshots for design review: `SCREENSHOT_DIR=<dir> dotnet test --project tests/ServiceBusConsole.Tests` dumps every page (`Screenshots.cs`), then `python tools/render-screenshots.py <dir>` renders PNGs (Pillow, macOS fonts).

## C# style

Modern, functional C#. Apply to new code and to code you touch.

- **Records for data.** `sealed record` with `required`/`init` properties (positional for small ones); change a value with `with`, never by setting properties. `readonly record struct` for small values (results, pairs, layout numbers). Data that crosses a boundary is immutable.
- **Classes for identity and lifecycle** only: services, components, state holders. `sealed` by default.
- **Expressions over statements.** Expression-bodied members, switch expressions, pattern matching, collection expressions (`[.. xs]`), LINQ and `IAsyncEnumerable` operators (`Select`, `Where`, `SelectMany`, `AnyAsync`), fluent builders (`services.AddX().AddY()`, `TableSort.Add(...)`). No loops that fill a list another operator could produce.
- **Loops where they read better:** channel pipelines, paging, retries, early exit with side effects.
- **Read-only at the edges:** expose `IReadOnlyList<T>`, accept `IEnumerable<T>`.
- **No `!`.** Make values non-null by construction: `required`, `where T : notnull`, pass the value instead of reading nullable state (e.g. an operation body receives its `OperationProgress`).

## Prerequisites

The repo pins the .NET 11 SDK (`global.json`); the app targets `net10.0` and also builds with SDK 10 (e.g. `dotnet run --project` from a folder pinning SDK 10). The submodule adds `net11.0` only under SDK 11; keep that condition.

Requires an active Azure CLI login before running:
```bash
az login
```

Authentication uses `AzureCliCredential` — no connection strings or keys are used.

## Architecture

This is a .NET 10 terminal UI app that browses Azure Service Bus namespaces, queues, topics, and messages. The UI runs entirely in the terminal using **RazorConsole.Core** — a library that renders Blazor-style Razor components to the terminal via Spectre.Console.

**Layout:** `src/ServiceBusConsole` (the UI, packed as the `azsb` tool), `src/ServiceBusConsole.Abstractions`, `src/ServiceBusConsole.AzureServiceBus`, `src/ServiceBusConsole.Plugins` (plugin contracts), bundled plugins `src/ServiceBusConsole.Plugins.<Name>`, a test project per tested project under `tests/`, `lib/RazorConsole` (fork submodule). Shared build settings in `Directory.Build.props`, package versions in `Directory.Packages.props` (central package management). One type per file; namespaces follow project and folders (`ServiceBusConsole.<Folder>`, `ServiceBusConsole.AzureServiceBus.Emulator`), imported globally via `<Using>` in the csproj and `_Imports.razor`.

**Projects:** the UI uses the abstractions and the plugin contracts only.
- `ServiceBusConsole.Abstractions`, no packages: DTOs (`QueueInfo`, `MessageInfo`, `MessageSource`, ...), `I*Service`, `ILockedMessages`, `MessageLockLostException`, `IServiceBusNamespace` (what every namespace has: `Info`, `Queues`, `Messages`), the feature interfaces, `INamespaceOpener`, `INamespaceDecorator`
- `ServiceBusConsole.AzureServiceBus`, the only project with Azure SDK packages: `NamespaceOpener` (`EmulatorNamespace`, or by `NamespaceInfo.Sku` `BasicNamespace` or `StandardNamespace` for Standard, Premium and an unknown SKU), the `I*Service` implementations (management API for Azure; config file and peeked counts for the emulator, in `Emulator/`), `LockedMessages`, `AsyncParallel`. Each namespace owns its `ServiceBusClient` and closes it on dispose. All internal except `AddAzureServiceBus()`
- Capabilities are composed, not checked: a namespace implements a feature interface or not (`IHasTopics`: not Basic; `IHasMetrics`, `IHasDeletion`: not the emulator; `IHasTransactions`: Standard and Premium only), and the UI asks `Connection.Current is IHasMetrics { Metrics: var metrics }`, hiding buttons and keys it lacks. Never branch on emulator or tier in the UI or throw `NotSupportedException` in a service; `AzureSubscription.IsEmulator` only picks the connection target and its icon
- Decorators wrap a namespace's services, not the namespace (a wrapper can't implement exactly the inner one's features): the opener applies every registered `INamespaceDecorator` while composing it
- Its SDK packages are `PrivateAssets="compile;..."`: the UI gets them at run time only, so SDK types do not compile there (`ArchitectureTests` checks it). Add new SDK packages the same way
- `ServiceBusMessageInfo` (Azure) derives from `MessageInfo` and keeps the received message, so Resend/Retry send exact copies (`IMessageService.SendCopiesAsync` takes only messages the service returned)
- Abstractions are values and streams: results over time are returned, as `IAsyncEnumerable<T>` (pages, running totals of send/purge; the work runs as the stream is read) or `ChannelReader<T>` (`ILockedMessages.Held`, `LockLost`). No callbacks, events or writer parameters; `CancellationToken` on every async member
- Variants are closed record unions with an exhaustive `Match` (`MessageSource.Queue` / `MessageSource.Subscription`), not nullable fields
- `ServiceBusConsole.Plugins`, plugin contracts on the abstractions; packages: DI and configuration abstractions only: `IPlugin`, `IMessageAction`, `MessageActionContext`, `IPluginHost`, `ActionResult`, `ProcessSpec`/`ProcessResult`, helpers `Template`, `PluginFiles`. No Azure SDK, RazorConsole or UI (`ArchitectureTests`)

| UI folder | Contents |
|---|---|
| `Program.cs` | Generic Host with `UseRazorConsole<App>()`, service registration (`AddAzureServiceBus()`), file logging to `logs/`; runs the command line through `AppCommands` |
| `CommandLine/` | JasperFx commands: `AppCommands` (`run` by default, `config`, `plugins`, `help`), `RunCommand`/`RunInput` (`--subscription`, `--namespace`), `StartupTarget` (taken once by the connection page via `NavigationState.Startup`) |
| `Configuration/` | `AppPaths` (config file, state directory), `ConfigFile` (dotted keys as nested JSON, read on every call), `ConfigKeys` (the known `ConfigKey`s), `AddAzsbConfiguration` (file and `AZSB_` environment variables into the host's `IConfiguration`); `azsb config` is `CommandLine/ConfigCommand` |
| `Pages/` | One Razor component per screen, each with a `@page` route |
| `Components/` | Shared Razor components; `App.razor` is the root router |
| `PluginHost/` | `PluginCatalog` (discovery, enabled, config keys), `AddPlugins`, `PluginActions` (offered actions and their context), `ProcessPluginHost`; namespace `ServiceBusConsole.PluginHost`, since `ServiceBusConsole.Plugins` is the contracts' |
| `UI/` | Non-component UI code: `Theme`/`Icons`, `TableSort`, `LiveList`, `CountChanges`, `NavigationState` (parameters that aren't URL segments), `FullscreenLayout` |
| `Services/` | `ServiceBusConnection` (the namespace being browsed: `SetNamespace` opens it and disposes the previous one; read `Current` each time), `SubscriptionCatalog`, `AzureSubscriptions` (emulator first, then the account's subscriptions) |
| `Services/Caching/` | `FileCache`, `CachingNamespaceDecorator` (wraps each opened namespace's listings, metrics and deletion: cached data first, then live), `CachingNamespaceService` |
| `Messaging/` | `MessageBrowser` (+ store), `OperationProgress`, `RetryText` |
| `Logging/`, `Syntax/` | File logger, JSON highlighting grammar |

**UI pattern:** Use `/razorconsole` for a full component and pattern reference. Namespace pages use `<FullscreenPage>`: `<Header>` (breadcrumb + count, rendered inside `ConnectionHeader`), `<Content>` (table wrapped in `<Scrollable>`), `<Footer>` (action buttons). `ConnectionHeader` shows subscription › namespace with click-to-switch pickers; switching calls `ServiceBusConnection.SetNamespace`, then `NavigationState.Reload()` remounts the page when it stays on the same route (`App.razor` keys `RouteView` on `NavState.PageVersion`). Navigation uses `Nav.NavigateTo("/route")`.

**Focus:** request focus once with `FocusManager.FocusAsync`; it keeps the request until the target renders. A retry loop can replace a later request (e.g. a dialog's OK) with one for an element that is gone.

**Configuration:** every setting is a `ConfigKey` registered in `ConfigKeys` (plugins' keys live under `plugins.`); read it from `IConfiguration` by `ConfigurationPath`. Precedence: command-line flag > `AZSB_` environment variable > config file > the key's default. Tests point the file elsewhere (`ConfigInput.File`, a temp path), never at the user's config.

**Theme:** colors and icons come from `Theme` and `Icons` (`UI/`), chosen by meaning (danger, warning, muted, ...). `Theme` reads the `Palette` picked at startup (`--theme auto|dark|light`, else the `ui.theme` setting; auto reads `COLORFGBG`, else dark). Palettes use only the terminal's 16 palette colors (`Color.Blue`, `Color.Navy`, ...) so the user's color scheme sets the shade; no RGB or 256-color values except the changed-count fade. Never hard-code a color in a component; add a palette entry. Selected rows paint `Theme.OnSelection` over all their text (`data-selected-fg`). JSON highlighting uses `PaletteSyntaxTheme`. `SCREENSHOT_THEME=light` dumps the light palette; `tools/update-screenshots.sh` renders both Counts use `<CountCell>` (muted when zero). Only use emoji with default emoji presentation: ones that need U+FE0F (⚠️, 🗑️, ⬅️) are one cell wide in some terminals and break table alignment.

**List pages:** rows live in a `LiveList<T>` (`UI/LiveList.cs`): first load shows cached then live rows, then it polls every `LiveCountsOptions.Interval` (15 s; keep it slow for Service Bus throttling) and `CountChanges<T>` highlights changed counts (`<CountCell Highlight=...>`) for 2 s. Azure listings use the paged `Get*RuntimePropertiesAsync` calls, one request per 100 entities.

**Syntax highlighting:** `src/ServiceBusConsole/Syntax/JsonLanguage.cs` replaces ColorCode's JSON grammar, which hangs on arrays of long strings (`AddSyntaxLanguages`, also called by `AppHarness`). Remove it once a ColorCode.Core release includes CommunityToolkit/ColorCode-Universal PR #46.

**Azure subscriptions** are listed from Azure with the signed-in `az` account (`INamespaceService.GetSubscriptionsAsync`, cached like namespaces) and shown by display name; the local emulator is always the first entry (`AzureSubscription.Emulator`).

**Metrics:** `MetricsPanel` (bottom of the list pages and of the connection page, below the lists) charts Azure Monitor metrics from `IMetricsService` (`Azure.ResourceManager.Monitor` on the shared `ArmClient`, `EntityName` filter; subscriptions show their topic, Azure has no per-subscription dimension). It loads after the selection settles, cached first (keyed by span and interval).
- Always `FullscreenLayout.MetricsPanel` lines, whatever it shows, so nothing around it moves. The connection page reserves it by terminal height and subscription count only, with a placeholder for the emulator
- `+`/`=` zoom in, `-`/`_` zoom out (or the `[+]`/`[-]` buttons), stepping `MetricsRange` between 1 h and 30 d; `-` and `_` go into a non-empty list filter instead. The interval is the finest Azure Monitor grain with at most 1.5 points per plot column (`MetricsChart.Interval`)
- F12 toggles it; range and F12 choice are remembered in the file cache. No panel for the emulator on list pages, or on short terminals

**Async commands:** Confirm-then-execute flows (delete, purge, requeue, etc.) use the `<AsyncCommand<TItem>>` component (`Components/AsyncCommand.razor`). It owns the pending-item, busy, and error state — pages should NOT reintroduce `_pending*` bool/field patterns or inline `ConfirmModal` + `ErrorModal` pairs per action.

Declare once per action and trigger via `@ref`:

```razor
<AsyncCommand @ref="_delete" TItem="MessageInfo"
              ConfirmMessage="@(m => $"Delete #{m.SequenceNumber}?")"
              Execute="@(m => MessageService.DeleteMessageAsync(Source, m.SequenceNumber))"
              OnCompleted="@(m => { _messages.Remove(m); StateHasChanged(); return Task.CompletedTask; })" />
```

- Omit `ConfirmMessage` to run without a confirm prompt.
- `TItem` is the thing acted on and never null (`where TItem : notnull`): the focused name, the message, or the `MessageSource` for a page-wide Purge. Invoke only when it exists: `if (_focusedItem is { } item) _cmd?.Invoke(item)`.
- `Execute` may return any `Task`/`Task<T>`; exceptions are caught and shown in the component's own `ErrorModal`.
- A page may still keep its own `_error`/`_loading` + `ErrorModal` for non-command paths like initial load (`PeekMessages`, streaming list loads) — those aren't async commands.

**Session context:** `SessionContext` (`Services/`) builds a `SessionSnapshot` (Abstractions) for plugins and LLM prompts: Azure subscription, namespace, source, browse mode, the message in view (open on the detail page, else under the list cursor) and the recent activity. `ToMarkdown()`/`ToJson()` format it (`MessageMarkdown`/`MessageJson` for a message alone), culture invariant with ISO 8601 times.
- `ActivityLog` keeps the last 200 `ActivityEntry`s, thread-safe. Recorded centrally: navigation (`App.razor`), message loads and batch operations (`MessageBrowserStore`, from `MessageBrowser.Ended`), every `ErrorModal` shown, and `AsyncCommand`s with an `Activity` text
- A new user-visible operation must record its outcome: run it through `MessageBrowser`, give its `AsyncCommand` an `Activity`, or call `ActivityLog.Record`

**Plugins:** bundled features that act on a message, a separate axis from namespace capabilities: resolved from DI, filtered by config. Writing one:
- Project `src/ServiceBusConsole.Plugins.<Name>` (namespace the same), referencing only `ServiceBusConsole.Plugins`; never the UI, Azure SDK or RazorConsole. The app's wildcard `ProjectReference` bundles it into the tool folder; add it and its `tests/ServiceBusConsole.Plugins.<Name>.Tests` to the slnx
- `[assembly: ServiceBusConsolePlugin(typeof(XPlugin))]`; `XPlugin : IPlugin` with a parameterless constructor, a lower-case `Id`, its `Settings` (`plugins.<id>.<key>`, `IsSecret` for tokens) and `ConfigureServices` registering its `IMessageAction`s. Disabled (`plugins.<id>.enabled=false`) registers nothing
- `IMessageAction.CanRun` is cheap; `RunAsync` gets a `MessageActionContext` (message, source, `SessionSnapshot`, settings with defaults, `IPluginHost`, `Progress`), throws on failure, honors the token (Esc) and returns an `ActionResult` (text, optional link and file)
- Processes and opening files or URLs go through `IPluginHost` (fake in tests); files under its `StateDirectory` via `PluginFiles`: message exports in `messages/`, the rest in `plugins/<id>/`
- The host shows the F2 picker, progress, result and error, and records the outcome in the activity log; plugins don't
- UI tests: `AppHarness.Plugins`, `.Settings` (`plugins:<id>:enabled`) and `.PluginHost` (`FakePluginHost` records processes and opened paths)

**Message operations:** `Components/MessageBrowserView.razor` is the shared queue/subscription browser. State lives in `MessageBrowser` (list, selection, locked receiver), kept by the singleton `MessageBrowserStore` so the detail page acts on the same locks.
- *Peek* is read-only; on an active queue *Resend* sends copies.
- *Receive (PeekLock)* returns `ILockedMessages`: the receiver stays open, locks auto-renew, leftovers are abandoned on dispose. Complete/abandon/dead-letter only work on these.
- *Receive (ReceiveAndDelete)* removes messages at once; *Requeue* resends them.
- *Retry* (DLQ, `MessageBrowser.RetryAsync`): chunks of up to 100, in order, as they are held. With `IHasTransactions` (Standard, Premium) each chunk moves in one transaction (`ITransactionService.MoveAsync`: send the copies, complete the originals; a failure rolls back and the chunk stays held); without, copies are sent, then the originals completed (at least once). Peeked DLQ messages are locked first with `IMessageService.StartLocking`, which receives from the front and abandons what it passes.
- Transactions use the namespace's own client: a dead-letter queue and its queue, or a subscription and its topic, are one top-level entity. Don't set `EnableCrossEntityTransactions`: it makes a client fail receives on every entity after the first
- Batch operations end in `OperationDialog` with a result (failures included) until OK; navigate in its `OnDismiss`, not in `AsyncCommand.OnCompleted`.
- That scan is only allowed in a dead-letter queue (abandon there only bumps the delivery count). Never scan an active queue: abandon there can dead-letter unrelated messages.
