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
dotnet test --project tests/ServiceBusConsole.Tests  # Headless UI tests
```

## Tests

`tests/ServiceBusConsole.Tests` mounts the real `App` in RazorConsole.Testing's `HeadlessTerminal` with fake services (`Fakes.cs`); no Azure or emulator needed.
- Start through `AppHarness.OpenQueuesAsync()` (clicks through the connection page). Navigating `NavigationManager` from the test thread does not render.
- Pages focus their table after rendering; call `AppHarness.WaitForTableFocusAsync` before sending keys to a new page.
- RazorConsole bugs found by tests are fixed in the fork (`azsb`), not worked around in the app.
- Keys go only to the focused element; they do not bubble. Page `HandleKeyDown` runs only via components that forward `OnKeyDown`.
- `AppHarness` gives each test its own `FileCache` directory under the temp folder.
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

Requires an active Azure CLI login before running:
```bash
az login
```

Authentication uses `AzureCliCredential` — no connection strings or keys are used.

## Architecture

This is a .NET 10 terminal UI app that browses Azure Service Bus namespaces, queues, topics, and messages. The UI runs entirely in the terminal using **RazorConsole.Core** — a library that renders Blazor-style Razor components to the terminal via Spectre.Console.

**Layout:** `src/ServiceBusConsole` (the app, packed as the `azsb` tool), `tests/ServiceBusConsole.Tests`, `lib/RazorConsole` (fork submodule). Shared build settings in `Directory.Build.props`, package versions in `Directory.Packages.props` (central package management). One type per file; namespaces follow folders (`ServiceBusConsole.<Folder>`), imported globally via `<Using>` in the csproj and `_Imports.razor`.

| Folder | Contents |
|---|---|
| `Program.cs` | Generic Host with `UseRazorConsole<App>()`, service registration, file logging to `logs/` |
| `Pages/` | One Razor component per screen, each with a `@page` route |
| `Components/` | Shared Razor components; `App.razor` is the root router |
| `UI/` | Non-component UI code: `Theme`/`Icons`, `TableSort`, `LiveList`, `CountChanges`, `NavigationState` (parameters that aren't URL segments), `FullscreenLayout` |
| `Models/` | DTOs: `QueueInfo`, `MessageInfo`, `MessageSource`, ... |
| `Services/` | `I*Service` + Azure implementations, `ServiceBusConnection`, `SubscriptionCatalog` |
| `Services/Caching/` | `FileCache` and the caching decorators: cached data first, then live |
| `Messaging/` | `MessageBrowser` (+ store), `LockedMessages`, `OperationProgress`, `RetryText` |
| `Emulator/` | Emulator `Config.json` model and reader |
| `Logging/`, `Threading/`, `Syntax/` | File logger, `AsyncParallel`, JSON highlighting grammar |

**UI pattern:** Use `/razorconsole` for a full component and pattern reference. Namespace pages use `<FullscreenPage>`: `<Header>` (breadcrumb + count, rendered inside `ConnectionHeader`), `<Content>` (table wrapped in `<Scrollable>`), `<Footer>` (action buttons). `ConnectionHeader` shows subscription › namespace with click-to-switch pickers; switching calls `ServiceBusConnection.Switch`, which remounts the page (`App.razor` keys `RouteView` on `Connection.Version`). Navigation uses `Nav.NavigateTo("/route")`.

**Theme:** colors and icons come from `Theme` and `Icons` (`UI/`), chosen by meaning (danger, warning, muted, ...). Counts use `<CountCell>` (muted when zero). Only use emoji with default emoji presentation: ones that need U+FE0F (⚠️, 🗑️, ⬅️) are one cell wide in some terminals and break table alignment.

**List pages:** rows live in a `LiveList<T>` (`UI/LiveList.cs`): first load shows cached then live rows, then it polls every `LiveCountsOptions.Interval` (15 s; keep it slow for Service Bus throttling) and `CountChanges<T>` highlights changed counts (`<CountCell Highlight=...>`) for 2 s. Azure listings use the paged `Get*RuntimePropertiesAsync` calls, one request per 100 entities.

**Syntax highlighting:** `src/ServiceBusConsole/Syntax/JsonLanguage.cs` replaces ColorCode's JSON grammar, which hangs on arrays of long strings (`AddSyntaxLanguages`, also called by `AppHarness`). Remove it once a ColorCode.Core release includes CommunityToolkit/ColorCode-Universal PR #46.

**Azure subscriptions** are listed from Azure with the signed-in `az` account (`INamespaceService.GetSubscriptionsAsync`, cached like namespaces) and shown by display name; the local emulator is always the first entry (`AzureSubscription.Emulator`).

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

**Message operations:** `Components/MessageBrowserView.razor` is the shared queue/subscription browser. State lives in `MessageBrowser` (list, selection, locked receiver), kept by the singleton `MessageBrowserStore` so the detail page acts on the same locks.
- *Peek* is read-only; on an active queue *Resend* sends copies.
- *Receive (PeekLock)* returns `ILockedMessages`: the receiver stays open, locks auto-renew, leftovers are abandoned on dispose. Complete/abandon/dead-letter only work on these.
- *Receive (ReceiveAndDelete)* removes messages at once; *Requeue* resends them.
- *Retry* (DLQ, `MessageBrowser.RetryAsync`): send copies in order, then complete the originals. Peeked DLQ messages are locked first with `IMessageService.LockAsync`, which receives from the front and abandons what it passes.
- Batch operations end in `OperationDialog` with a result (failures included) until OK; navigate in its `OnDismiss`, not in `AsyncCommand.OnCompleted`.
- That scan is only allowed in a dead-letter queue (abandon there only bumps the delivery count). Never scan an active queue: abandon there can dead-letter unrelated messages.
