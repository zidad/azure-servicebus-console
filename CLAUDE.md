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
- Need a RazorConsole component to behave differently? Copy it into `Components/` and change the copy; never change the fork's components
- The fork only holds engine fixes (layout, rendering, input) and the smallest hooks the app cannot add itself, each with an upstream PR or one to open; drop it once upstream has it

With every change:

- Rebase that commit onto the latest upstream main; where upstream merged one of our PRs or an equivalent fix, keep upstream's version
- Check each remaining customization for an upstream replacement; remove the patch and the app workaround when one exists
- New fork changes: commit, then fold back into one commit (`git reset --soft upstream/main && git commit`); the message lists the customizations by area
- Upstream PRs come from separate `fix/*` branches off upstream main, never from `azsb`
- Run the fork and app tests, push `azsb` with `--force-with-lease`, bump the submodule pointer in the same PR

## Releases

Release by pushing a version tag on a `main` commit: `git tag v1.2.3 origin/main && git push origin v1.2.3`. `publish.yml` then tests, packs with the tag's version (`tool-packages.yml`), pushes to NuGet and creates a GitHub release with the native archives, their checksums and generated notes. There is no version in the code or in PR titles.

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
- Click with `AppHarness.ClickTextAsync`/`ClickSettledAsync`: they click once the target has stayed on the same cell for `SettleTime`, since rows loading in the background move it. Before keys to a page that reloads in place (F6, a new peek), `AppHarness.WaitForSettledAsync`: screen and focus unchanged for `SettleTime`. CI's `ui-stress` job repeats the UI tests on 2 cores
- Dialogs take focus one render after their text appears: `AppHarness.WaitForFocusAsync(terminal, focusOrder)` before typing into one.
- RazorConsole bugs found by tests are fixed in the fork (`azsb`), not worked around in the app.
- Keys go only to the focused element; they do not bubble. Page `HandleKeyDown` runs only via components that forward `OnKeyDown`.
- `AppHarness` gives each test its own `FileCache` directory under the temp folder.
- UI tests run at most one per core (`TestParallelism.cs`), and one at a time per class: a new UI test class gets `[NotInParallel(nameof(TheClass))]`. TUnit's defaults (4 per core, tests of a class in parallel) starve their render timeouts.
- Screenshots for design review: `SCREENSHOT_DIR=<dir> dotnet test --project tests/ServiceBusConsole.Tests` dumps every page (`Screenshots.cs`), then `python tools/render-screenshots.py <dir>` renders PNGs (Pillow; macOS fonts, else JetBrains Mono or DejaVu and Noto Color Emoji).

## C# style

Modern, functional C#. Apply to new code and to code you touch.

- **Records for data.** `sealed record` with `required`/`init` properties (positional for small ones); change a value with `with`, never by setting properties. `readonly record struct` for small values (results, pairs, layout numbers). Data that crosses a boundary is immutable.
- **Classes for identity and lifecycle** only: services, components, state holders. `sealed` by default.
- **Expressions over statements.** Expression-bodied members, switch expressions, pattern matching, collection expressions (`[.. xs]`), LINQ and `IAsyncEnumerable` operators (`Select`, `Where`, `SelectMany`, `AnyAsync`), fluent builders (`services.AddX().AddY()`, `TableSort.Add(...)`). No loops that fill a list another operator could produce.
- **Loops where they read better:** channel pipelines, paging, retries, early exit with side effects.
- **Read-only at the edges:** expose `IReadOnlyList<T>`, accept `IEnumerable<T>`.
- **No `!`.** Make values non-null by construction: `required`, `where T : notnull`, pass the value instead of reading nullable state (e.g. an operation body receives its `OperationProgress`).

## Native AOT

The app stays publishable with `PublishAot=true`: no reflection the trimmer can't see.
- JSON through source-generated contexts: a new cached type goes in `CacheJsonContext` (`FileCache` throws for others); a generated context resets a missing `init` property to null, so give defaults with `set` where JSON may leave it out (`EmulatorJsonContext`)
- No assembly scanning or loading: plugins are listed in `Bundled.Plugins`, services registered explicitly (`Decorate<TService, TDecorator>` instead of Scrutor)
- Blazor's router scans the app assembly for `@page` components: it is a `TrimmerRootAssembly`
- Shipped as a RID-specific tool (`ToolPackageRuntimeIdentifiers`): `Net.Azure.ServiceBusConsole` points to a Native AOT package per platform (`dotnet pack -r <rid>`, each on its own OS) and the framework-dependent `.any` (a plain `dotnet pack`, which also makes the top-level package). `tool-packages.yml` packs them all, on every PR too, with an archive per platform for the release; NuGet gets the platform packages before the top-level one
- Libraries are `IsAotCompatible` (`Directory.Build.props`), so trim and AOT warnings show in a normal build
- A local native pack on macOS with Homebrew's OpenSSL needs `LIBRARY_PATH=/opt/homebrew/opt/openssl@3/lib:/opt/homebrew/opt/brotli/lib:/opt/homebrew/opt/zstd/lib`

## Prerequisites

The repo pins the .NET 11 SDK (`global.json`); everything targets `net11.0`. ASP.NET Core 11's BL0012 is off: handlers here also run from forwarded keys, outside their own component's event.

Requires an active Azure CLI login before running:
```bash
az login
```

Authentication uses the Azure CLI's login by default — no connection strings or keys are used. `azure.auth` (`cli`, `browser`, `default`) and `azure.transport` (`tcp`, `websockets`) become the registered `AzureOptions` (`Configuration/AzureSettings`; an unknown value is a usage error); `AddAzureServiceBus` uses `new AzureOptions()` when none is registered.

## Architecture

This is a .NET 11 terminal UI app that browses Azure Service Bus namespaces, queues, topics, and messages. The UI runs entirely in the terminal using **RazorConsole.Core** — a library that renders Blazor-style Razor components to the terminal via Spectre.Console.

**Layout:** `src/ServiceBusConsole` (the UI, packed as the `azsb` tool), `src/ServiceBusConsole.Core`, `src/ServiceBusConsole.Abstractions`, `src/ServiceBusConsole.AzureServiceBus`, `src/ServiceBusConsole.Plugins` (plugin contracts), bundled plugins `src/ServiceBusConsole.Plugins.<Name>`, listed in `src/ServiceBusConsole.BundledPlugins`, a test project per tested project under `tests/`, `lib/RazorConsole` (fork submodule). Shared build settings in `Directory.Build.props`, package versions in `Directory.Packages.props` (central package management). One type per file; namespaces follow project and folders (`ServiceBusConsole.<Folder>`, `ServiceBusConsole.AzureServiceBus.Emulator`), imported globally via `<Using>` in the csproj and `_Imports.razor`.

**Projects:** the UI uses the abstractions, Core and the plugin contracts only.
- `ServiceBusConsole.Core`: what any front end shares (the terminal UI now, a MAUI app later): `Messaging/` (`MessageBrowser` + store, `OperationProgress`, `RetryText`), `Services/` (`ServiceBusConnection`, `SubscriptionCatalog`, `AzureSubscriptions`, `RecentNamespaces`, `ActivityLog`), `Services/Caching/`, and UI-agnostic helpers in `UI/` (`DialogField`, `ErrorText`, `RenderThrottle`). Namespaces `ServiceBusConsole.Core.<Folder>`, its own `Resources/`. No RazorConsole, Spectre.Console, ASP.NET Core or Azure SDK (`ArchitectureTests`); code that needs them, or `NavigationState`, stays in the UI
- `ServiceBusConsole.Abstractions`, no packages: DTOs (`QueueInfo`, `MessageInfo`, `MessageSource`, ...), `I*Service`, `ILockedMessages`, `MessageLockLostException`, `IServiceBusNamespace` (what every namespace has: `Info`, `Queues`, `Messages`), the feature interfaces, `INamespaceOpener`, `INamespaceDecorator`
- `ServiceBusConsole.AzureServiceBus`, the only project with Azure SDK packages: `AzureOptions` (`AzureSignIn`, `ServiceBusTransport`; `Credentials` maps them to the `TokenCredential` and `ServiceBusClientOptions`), `NamespaceOpener` (`EmulatorNamespace`, or by `NamespaceInfo.Sku` `BasicNamespace` or `StandardNamespace` for Standard, Premium and an unknown SKU), the `I*Service` implementations (management API for Azure; config file and peeked counts for the emulator, in `Emulator/`), `LockedMessages`, `AsyncParallel`. Each namespace owns its `ServiceBusClient` and closes it on dispose. All internal except `AddAzureServiceBus()` and `AzureOptions` with its enums
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
| `CommandLine/` | System.CommandLine: `AppCommands` parses into a `CommandInput` (`RunInput`, `ConfigInput`, `PluginsInput`; `Bind` in tests) and runs `RunCommand` (default; `--subscription`, `--namespace`, `--open`, `--theme`), `ConfigCommand` or `PluginsCommand`; `help`. `StartupTarget` is taken once by the connection page via `NavigationState.Startup`; `--open` resolves with `StartupDestination.Find` over `JumpTargets`. Namespace commands (`check`, `list`, `peek`, `retry`, `delete`, `purge`, `send`) bind to a `NamespaceInput` whose `NamespaceCommand` runs on `CommandServices` (live Azure, no cache; fakes in `NamespaceCommandTests`): text or `--json` on stdout, errors on stderr, `ExitCodes`. Retry and delete go through `MessageBrowser`; `cli.protect` and `cli.redact.*` guard namespaces |
| `Configuration/` | `AppPaths` (config file, state directory), `AzureSettings` (`azure.*` keys as `AzureOptions`), `ConfigFile` (dotted keys as nested JSON, read on every call), `ConfigKeys` (the known `ConfigKey`s), `AddAzsbConfiguration` (file and `AZSB_` environment variables into the host's `IConfiguration`); `azsb config` is `CommandLine/ConfigCommand` |
| `Pages/` | One Razor component per screen, each with a `@page` route |
| `Components/` | Shared Razor components; `App.razor` is the root router |
| `PluginHost/` | `PluginCatalog` (discovery, enabled, config keys), `AddPlugins`, `PluginActions` (offered actions and their context), `ProcessPluginHost`; namespace `ServiceBusConsole.PluginHost`, since `ServiceBusConsole.Plugins` is the contracts' |
| `UI/` | Non-component UI code: `Theme`/`Icons`, `TableSort`, `LiveList`, `CountChanges`, `NavigationState` (parameters that aren't URL segments), `FullscreenLayout` |
| `Services/` | `NamespaceSwitcher` (opening a namespace: switch, record in `RecentNamespaces`, navigate; tree, startup and jump), `SessionContext`, `Clipboard`. In Core: `ServiceBusConnection` (the namespace being browsed: `SetNamespace` opens it and disposes the previous one; read `Current` each time), `SubscriptionCatalog`, `AzureSubscriptions` (emulator first, then the account's subscriptions) |
| `Services/Caching/` (Core) | `FileCache`, `CachingNamespaceDecorator` (wraps each opened namespace's listings, metrics and deletion: cached data first, then live), `CachingNamespaceService` |
| `Logging/`, `Syntax/` | File logger, JSON highlighting grammar |

**UI pattern:** Use `/razorconsole` for a full component and pattern reference. Namespace pages use `<FullscreenPage>`: `<Header>` (breadcrumb + count, rendered inside `ConnectionHeader`), `<Status>` (right of the breadcrumb: `<LiveStatus>` on list pages), `<Content>` (table wrapped in `<Scrollable>`), `<Footer>` (action buttons). `ConnectionHeader` is one line (`azsb │ subscription › namespace › page`) and a rule, with click-to-switch pickers on the subscription and namespace; switching calls `ServiceBusConnection.SetNamespace`, then `NavigationState.Reload()` remounts the page when it stays on the same route (`App.razor` keys `RouteView` on `NavState.PageVersion`). Navigation uses `Nav.NavigateTo("/route")`. The section list pages (queues, topics, all subscriptions) start their content with `<SectionTabs>`: keys 1 2 3, the page's own count live, the others read from the cached listings (no requests), plus SKU · region · resource group.

**Tables:** a page's main table is its bordered box: `Border="TableBorder.Rounded" data-column-separators="false"` (a fork hook) draws the outer border and the header rule, no lines between columns. List pages end their rows with `<FillerRows>` so the box keeps a full page's height; the message browser doesn't, its preview takes the spare lines. A `<DeadLetterMark>` gutter marks rows with dead letters.

**Text:** everything a user reads comes from `.resx` files in the project's `Resources/` folder, one strongly typed class per file (`Microsoft.CodeAnalysis.ResxSourceGenerator`, set up in `Directory.Build.props` for any project with a `Resources/` folder). English only for now; a language is added as `<File>.<culture>.resx`.
- UI: `PageStrings` (Pages/), `ComponentStrings` (Components/), `<Folder>Strings` for the other folders (`CommandLineStrings`, `ConfigurationStrings`, ...), `Strings` for words shared across areas. Plugins, the plugin contracts and the Azure project each have their own `Resources/Strings.resx`
- Names are `<Owner>_<What>`: the page, component or class that shows it (`QueueList_Empty`, `HelpDialog_Title`); shared ones in `Strings` have no owner
- Placeholders are named (`{entity}`), which generates `FormatX(entity)`; not a C# keyword (`{namespaceName}`, not `{namespace}`: the generator doesn't escape it); no format specifiers in the resource (format the value in code), pluralization and conditions stay in code choosing between resources
- Text that is also matched or used as a name (`AzureSubscription.Emulator` for `--subscription`, `MessageSource.DisplayName` in plugin file names) must keep working when translated
- Text a user can override with placeholders of their own (`ClaudePrompt.Default`) and generated documents for tools (`SessionMarkdown`, issue bodies) stay in code
- In scope: labels, key hints, titles, dialogs, status, errors shown in the UI or CLI (including exception messages that reach `ErrorModal`), CLI help and output, plugin labels, settings descriptions and results. Not: log messages, config keys, routes, queries (KQL), CLI option names, icons (`Icons`), test expectations (tests keep literal English, so they catch text changes)

**Keys:** a page lists its keys once as `KeyBinding`s (`Keys` property); `KeyHints` renders the footer from them, `HelpDialog` (`?`) lists them all and `KeyBinding.Dispatch` runs them from `HandleKeyDown`, after `AsyncCommand.HandleKey` (y/n) and the filter. Letters are commands, Shift for anything that deletes or moves messages (`D`, `P`, `R`, `X`), `^r` refresh; each F-key of earlier versions stays as an `Aliases` entry. `:` opens `JumpDialog` on the section lists and the browser: `JumpIndex.Search` (pure, prefix > contains > letters in order, dead letters first) over the namespace's queues, topics and subscriptions, from the cached listings or live when not cached, shown by kind (`JumpIndex.Grouped`); `⏎` opens the Active tab, `⇥` the dead-letter queue. A match with dead letters lists actions (`^p` purge, `^r` retry): they open its dead-letter browser with `NavigationState.StartCommand`, which runs its own purge or retry-all after the first peek. Other namespaces (cached listings of `SubscriptionCatalog`'s subscriptions) are targets too, and `^a` adds their cached entities (`JumpTargets.CachedAsync`, never live); opening one switches the connection like the header picker. Lists filter on `/` (`ListFilter`: typing edits, Enter keeps, Escape clears; `NavigableTBody.TakeKey` lets Enter end the edit); only the header dropdown filters as you type. `KeyButton` doesn't forward Space or Enter to the page: they click it.

**Focus:** request focus once with `FocusManager.FocusAsync`; it keeps the request until the target renders. A retry loop can replace a later request (e.g. a dialog's OK) with one for an element that is gone.

**Configuration:** every setting is a `ConfigKey` registered in `ConfigKeys` (plugins' keys live under `plugins.`); read it from `IConfiguration` by `ConfigurationPath`. Precedence: command-line flag > `AZSB_` environment variable > config file > the key's default. Tests point the file elsewhere (`ConfigInput.File`, a temp path), never at the user's config.

**Theme:** colors and icons come from `Theme` and `Icons` (`UI/`), chosen by meaning (danger, warning, muted, ...). `Theme` reads the `Palette` picked at startup (`--theme auto|dark|light`, else the `ui.theme` setting; auto reads `COLORFGBG`, else dark). Palettes use only the terminal's 16 palette colors (`Color.Blue`, `Color.Navy`, ...) so the user's color scheme sets the shade; no RGB or 256-color values except the changed-count fade. Never hard-code a color in a component; add a palette entry. Selected rows paint `Theme.OnSelection` over all their text (`data-selected-fg`). JSON highlighting uses `PaletteSyntaxTheme`. `SCREENSHOT_THEME=light` dumps the light palette; `tools/update-screenshots.sh` renders both Counts use `<CountCell>` (muted when zero). Only use emoji with default emoji presentation: ones that need U+FE0F (⚠️, 🗑️, ⬅️) are one cell wide in some terminals and break table alignment.

**List pages:** rows live in a `LiveList<T>` (`UI/LiveList.cs`): first load shows cached then live rows, then it polls every `LiveCountsOptions.Interval` (15 s; keep it slow for Service Bus throttling) and `CountChanges<T>` highlights changed counts (`<CountCell Highlight=...>`) for 2 s. Azure listings use the paged `Get*RuntimePropertiesAsync` calls, one request per 100 entities.

**Syntax highlighting:** `src/ServiceBusConsole/Syntax/JsonLanguage.cs` replaces ColorCode's JSON grammar, which hangs on arrays of long strings (`AddSyntaxLanguages`, also called by `AppHarness`). Remove it once a ColorCode.Core release includes CommunityToolkit/ColorCode-Universal PR #46.

**Azure subscriptions** are listed from Azure with the signed-in `az` account (`INamespaceService.GetSubscriptionsAsync`, cached like namespaces) and shown by display name; the local emulator is always the first entry (`AzureSubscription.Emulator`).

**Metrics:** `MetricsPanel` (bottom of the list pages and of the connection page, below the lists) charts Azure Monitor metrics from `IMetricsService` (`Azure.ResourceManager.Monitor` on the shared `ArmClient`, `EntityName` filter; subscriptions show their topic, Azure has no per-subscription dimension). It loads after the selection settles, cached first (keyed by span and interval).
- Charts are the app's own: `StepPlot` (`UI/`) draws the steps as colored runs, `StepChartView` renders them; not RazorConsole's `StepChart`
- Always `FullscreenLayout.MetricsPanel` lines, whatever it shows, so nothing around it moves. The connection page reserves it by terminal height and subscription count only, with a placeholder for the emulator
- `+`/`=` zoom in, `-`/`_` zoom out (or the `[+]`/`[-]` buttons), stepping `MetricsRange` between 1 h and 30 d; `-` and `_` go into a filter being edited instead. The interval is the finest Azure Monitor grain with at most 1.5 points per plot column (`MetricsChart.Interval`)
- The section lists add an `Incoming · 24 h` sparkline column (`IncomingTraffic`, `<Sparkline>`): `IMetricsService.GetIncomingByEntityAsync`, one Azure Monitor request per namespace (`EntityName eq '*'`, hourly), cached like the charts, reloaded every `LiveCountsOptions.TrafficInterval` (5 min). Never one request per entity. Subscriptions show their topic's; no column without `IHasMetrics` or under 110 columns
- `m` (F12) toggles it; range and F12 choice are remembered in the file cache. No panel for the emulator on list pages, or on short terminals

**Async commands:** Confirm-then-execute flows (delete, purge, requeue, etc.) use the `<AsyncCommand<TItem>>` component (`Components/AsyncCommand.razor`). It owns the pending-item, busy, and error state — pages should NOT reintroduce `_pending*` bool/field patterns or inline `ConfirmModal` + `ErrorModal` pairs per action.

Declare once per action and trigger via `@ref`:

```razor
<AsyncCommand @ref="_delete" TItem="MessageInfo"
              ConfirmMessage="@(m => $"Delete #{m.SequenceNumber}?")"
              Execute="@(m => MessageService.DeleteMessageAsync(Source, m.SequenceNumber))"
              OnCompleted="@(m => { _messages.Remove(m); StateHasChanged(); return Task.CompletedTask; })" />
```

- Omit `ConfirmMessage` to run without a confirm prompt. `ConfirmByTyping="true"` (purges) makes the user type yes; y alone doesn't confirm then.
- `TItem` is the thing acted on and never null (`where TItem : notnull`): the focused name, the message, or the `MessageSource` for a page-wide Purge. Invoke only when it exists: `if (_focusedItem is { } item) _cmd?.Invoke(item)`.
- `Execute` may return any `Task`/`Task<T>`; exceptions are caught and shown in the component's own `ErrorModal`.
- A page may still keep its own `_error`/`_loading` + `ErrorModal` for non-command paths like initial load (`PeekMessages`, streaming list loads) — those aren't async commands.

**Session context:** `SessionContext` (`Services/`) builds a `SessionSnapshot` (Abstractions) for plugins and LLM prompts: Azure subscription, namespace, source, browse mode, the message in view (open on the detail page, else under the list cursor) and the recent activity. `ToMarkdown()`/`ToJson()` format it (`MessageMarkdown`/`MessageJson` for a message alone), culture invariant with ISO 8601 times.
- `ActivityLog` keeps the last 200 `ActivityEntry`s, thread-safe. Recorded centrally: navigation (`App.razor`), message loads and batch operations (`MessageBrowserStore`, from `MessageBrowser.Ended`), every `ErrorModal` shown, and `AsyncCommand`s with an `Activity` text
- A new user-visible operation must record its outcome: run it through `MessageBrowser`, give its `AsyncCommand` an `Activity`, or call `ActivityLog.Record`

**Plugins:** bundled features that act on a message, a separate axis from namespace capabilities: resolved from DI, filtered by config. Writing one:
- Project `src/ServiceBusConsole.Plugins.<Name>` (namespace the same), referencing only `ServiceBusConsole.Plugins`; never the UI, Azure SDK or RazorConsole. Add `new XPlugin()` to `Bundled.Plugins` (`ServiceBusConsole.BundledPlugins`, whose wildcard `ProjectReference` picks up the project), and the project and its `tests/ServiceBusConsole.Plugins.<Name>.Tests` to the slnx
- `XPlugin : IPlugin` with a lower-case `Id`, its `Settings` (`plugins.<id>.<key>`, `IsSecret` for tokens) and `ConfigureServices` registering its `IMessageAction`s. Disabled (`plugins.<id>.enabled=false`) registers nothing
- `IMessageAction.CanRun` is cheap; `RunAsync` gets a `MessageActionContext` (message, source, `SessionSnapshot`, settings with defaults, `IPluginHost`, `Progress`), throws on failure, honors the token (Esc) and returns an `ActionResult` (text, optional link and file)
- Processes and opening files or URLs go through `IPluginHost` (fake in tests); files under its `StateDirectory` via `PluginFiles`: message exports in `messages/`, the rest in `plugins/<id>/`
- The host shows the `.` (F2) picker, progress, result and error, and records the outcome in the activity log; plugins don't
- UI tests: `AppHarness.Plugins`, `.Settings` (`plugins:<id>:enabled`) and `.PluginHost` (`FakePluginHost` records processes and opened paths)

**Message operations:** `Components/MessageBrowserView.razor` is the shared queue/subscription browser: `<SourceTabs>` (1 Active, 2 Dead-letter; counts from the cached listing) with the browse mode's badge on the right, a full-width strip in Locked and Received mode, relative times in the list, and `<MessagePreview>` as one bordered box (dead-letter reason and description first, then grouped properties beside the body). State lives in `MessageBrowser` (list, selection, locked receiver), kept by the singleton `MessageBrowserStore` so the detail page acts on the same locks.
- The detail page (`/message`) is the zoom view: tabs 1 Body, 2 Properties, 3 Application, 4 Raw (`MessageJson`), `[ ]` step through the browser's list (its cursor follows), `y` copies the body with OSC 52 (`Clipboard`; `AppHarness.Copied` in tests)
- *Peek* is read-only; on an active queue *Resend* sends copies.
- *Receive (PeekLock)* returns `ILockedMessages`: the receiver stays open, locks auto-renew, leftovers are abandoned on dispose. Complete/abandon/dead-letter only work on these. Settled rows and lost locks stay in the list, dimmed (`Theme.Settled`), with their `LockedState`; `MessageBrowser.CanAct` keeps them out of selections and commands.
- *Receive (ReceiveAndDelete)* removes messages at once; *Requeue* resends them.
- *Retry* (DLQ, `MessageBrowser.RetryAsync`): chunks of up to 100, in order, as they are held. With `IHasTransactions` (Standard, Premium) each chunk moves in one transaction (`ITransactionService.MoveAsync`: send the copies, complete the originals; a failure rolls back and the chunk stays held); without, copies are sent, then the originals completed (at least once). Peeked DLQ messages are locked first with `IMessageService.StartLocking`, which receives from the front and abandons what it passes.
- Transactions use the namespace's own client: a dead-letter queue and its queue, or a subscription and its topic, are one top-level entity. Don't set `EnableCrossEntityTransactions`: it makes a client fail receives on every entity after the first
- Retry, Resend, Requeue and Purge end in `OperationDialog` with a result (failures included) until OK; navigate in its `OnDismiss`, not in `AsyncCommand.OnCompleted`.
- Complete, abandon and dead-letter in Locked mode run without a dialog and report in a one-line status under the list (`MessageBrowser.Settled`; failures in the danger color), recorded through `Ended` like the rest; the detail page goes back to that list in their `OnCompleted`.
- That scan is only allowed in a dead-letter queue (abandon there only bumps the delivery count). Never scan an active queue: abandon there can dead-letter unrelated messages.
