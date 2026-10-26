# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Git workflow

Always fetch main and rebase before pushing a branch or opening a PR:

```bash
git fetch origin
git rebase origin/main
```

Resolve any conflicts, then push with `--force-with-lease`.

With every change, also rebase the fork's `azsb` branch (`lib/RazorConsole`) onto the latest upstream `RazorConsole/RazorConsole` main:

- Drop fork commits upstream now covers (merged PRs, equivalent fixes); git skips identical patches on its own
- Check each remaining customization for an upstream replacement; remove the patch and the app workaround when one exists
- Run the fork and app tests, push `azsb` with `--force-with-lease`, bump the submodule pointer in the same PR

## Releases

Release by pushing a version tag on a `main` commit: `git tag v1.2.3 origin/main && git push origin v1.2.3`. `publish.yml` then tests, packs with the tag's version, pushes to NuGet and creates a GitHub release with generated notes. There is no version in the code or in PR titles.

When a PR moves the `lib/RazorConsole` pointer, tag that fork commit `azsb-pr<PR number>` and push the tag to the fork, so later `azsb` rebases don't orphan it. (Older tags are `azsb-v<Version>`.)

## Commands

```bash
dotnet build                               # Build the project
dotnet run                                 # Run the console app
dotnet test tests/ServiceBusConsole.Tests  # Headless UI tests
```

## Tests

`tests/ServiceBusConsole.Tests` mounts the real `App` in RazorConsole.Testing's `HeadlessTerminal` with fake services (`Fakes.cs`); no Azure or emulator needed.
- Start through `AppHarness.OpenQueuesAsync()` (clicks through the connection page). Navigating `NavigationManager` from the test thread does not render.
- Pages focus their table after rendering; call `AppHarness.WaitForTableFocusAsync` before sending keys to a new page.
- RazorConsole bugs found by tests are fixed in the fork (`azsb`), not worked around in the app.
- Keys go only to the focused element; they do not bubble. Page `HandleKeyDown` runs only via components that forward `OnKeyDown`.
- `AppHarness` gives each test its own `FileCache` directory under the temp folder.
- Screenshots for design review: `SCREENSHOT_DIR=<dir> dotnet test tests/ServiceBusConsole.Tests` dumps every page (`Screenshots.cs`), then `python tools/render-screenshots.py <dir>` renders PNGs (Pillow, macOS fonts).

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

**UI pattern:** Use `/razorconsole` for a full component and pattern reference. Namespace pages use `<FullscreenPage>`: `<Header>` (breadcrumb + count, rendered inside `ConnectionHeader`), `<Content>` (table wrapped in `<Scrollable>`), `<Footer>` (action buttons). `ConnectionHeader` shows subscription › namespace with click-to-switch pickers; switching calls `ServiceBusConnection.Switch`, which remounts the page (`App.razor` keys `RouteView` on `Connection.Version`). Navigation uses `Nav.NavigateTo("/route")`.

**Theme:** colors and icons come from `Theme` and `Icons` (`Theme.cs`), chosen by meaning (danger, warning, muted, ...). Counts use `<CountCell>` (muted when zero). Only use emoji with default emoji presentation: ones that need U+FE0F (⚠️, 🗑️, ⬅️) are one cell wide in some terminals and break table alignment.

**List pages:** rows live in a `LiveList<T>` (`LiveList.cs`): first load shows cached then live rows, then it polls every `LiveCountsOptions.Interval` (15 s; keep it slow for Service Bus throttling) and `CountChanges<T>` highlights changed counts (`<CountCell Highlight=...>`) for 2 s. Azure listings use the paged `Get*RuntimePropertiesAsync` calls, one request per 100 entities.

**Syntax highlighting:** `Syntax/JsonLanguage.cs` replaces ColorCode's JSON grammar, which hangs on arrays of long strings (`AddSyntaxLanguages`, also called by `AppHarness`). Remove it once a ColorCode.Core release includes CommunityToolkit/ColorCode-Universal PR #46.

**Azure subscriptions** are listed from Azure with the signed-in `az` account (`INamespaceService.GetSubscriptionsAsync`, cached like namespaces) and shown by display name; the local emulator is always the first entry (`AzureSubscription.Emulator`).

**Async commands:** Confirm-then-execute flows (delete, purge, requeue, etc.) use the `<AsyncCommand<TItem>>` component (`Components/AsyncCommand.razor`). It owns the pending-item, busy, and error state — pages should NOT reintroduce `_pending*` bool/field patterns or inline `ConfirmModal` + `ErrorModal` pairs per action.

Declare once per action and trigger via `@ref`:

```razor
<AsyncCommand @ref="_delete" TItem="MessageInfo"
              ConfirmMessage="@(m => $"Delete #{m!.SequenceNumber}?")"
              Execute="@(m => MessageService.DeleteMessageAsync(Source, m!.SequenceNumber))"
              OnCompleted="@(m => { _messages.Remove(m!); StateHasChanged(); return Task.CompletedTask; })" />
```

- Omit `ConfirmMessage` to run without a confirm prompt.
- Use `TItem="object"` and call `_cmd!.Invoke()` (no arg) for page-scoped commands like Purge.
- `Execute` may return any `Task`/`Task<T>` (covariant via `Func<TItem?, Task>`); exceptions are caught and shown in the component's own `ErrorModal`.
- A page may still keep its own `_error`/`_loading` + `ErrorModal` for non-command paths like initial load (`PeekMessages`, streaming list loads) — those aren't async commands.

**Message operations:** `Components/MessageBrowserView.razor` is the shared queue/subscription browser. State lives in `MessageBrowser` (list, selection, locked receiver), kept by the singleton `MessageBrowserStore` so the detail page acts on the same locks.
- *Peek* is read-only; on an active queue *Resend* sends copies.
- *Receive (PeekLock)* returns `ILockedMessages`: the receiver stays open, locks auto-renew, leftovers are abandoned on dispose. Complete/abandon/dead-letter only work on these.
- *Receive (ReceiveAndDelete)* removes messages at once; *Requeue* resends them.
- *Retry* (DLQ, `MessageBrowser.RetryAsync`): send copies in order, then complete the originals. Peeked DLQ messages are locked first with `IMessageService.LockAsync`, which receives from the front and abandons what it passes.
- Batch operations end in `OperationDialog` with a result (failures included) until OK; navigate in its `OnDismiss`, not in `AsyncCommand.OnCompleted`.
- That scan is only allowed in a dead-letter queue (abandon there only bumps the delivery count). Never scan an active queue: abandon there can dead-letter unrelated messages.
