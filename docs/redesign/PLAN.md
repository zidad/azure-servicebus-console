# UI redesign plan

Mockups: https://claude.ai/artifact/UjxeBgmjH6dHtSwYCV3cuT (8 screens, dark and light).
Each phase below is one PR. Follow `CLAUDE.md` for style, tests and the fork rules; update the screenshots (`tools/update-screenshots.sh`) in every PR that changes what a page looks like.

## What changes and why

| Today | Redesign | Why |
|---|---|---|
| Boxed header, boxed tables with a line between every column, empty spacer rows | One-line breadcrumb + `─` rule; tables with a header, one rule under it, no column lines | About 4 more rows of data per page, much less noise |
| 6-line Figlet banner on the connection page | One header line with the app name and account summary | Banner costs a sixth of an 80×24 terminal |
| Emoji on every button, header and cell | Text, plus a few one-cell glyphs with a meaning: `●` dead letters/active, `○` disabled, `✓` done, `✖` error, `↺` abandoned, `▾ ▸` tree | Emoji widths vary between terminals, and colored emoji compete with the palette |
| F1–F12 | Letters, shift for anything that deletes or moves messages; F-keys stay as aliases | F1, F10, F11, F12 are taken by GNOME Terminal, Windows Terminal, VS Code and macOS; letters are easier to remember |
| Type-to-filter on every key | `/` opens the filter (as in less, vim, k9s, lazygit) | Needed so letters can be commands |
| `[F9] ❌ Delete` buttons | Key chips: `D Delete` (key on a gray chip, red chip for danger) | Shorter, footer fits on one line at 100 columns |
| Queues / Topics / All Subscriptions on F1/F2/F3 and a dropdown | Tab strip `Queues 5 · Topics 3 · Subscriptions 7`, keys `1 2 3` and `⇥` | You can see where you are and what else exists |
| F6 toggles Active/DLQ; mode is `👀 peek` in the breadcrumb | Tabs `Active 1.284 · Dead-letter 3` + a badge `PEEK`; a full-width yellow strip `LOCKED`, red for `RECEIVED AND DELETED` | Locked and deleted modes need to be loud |
| Preview: half the screen, mostly empty Properties box, reason cut off | Dead-letter reason + description on one line, compact properties left (grouped System / Application), body with line numbers right | Reason is the first thing you look for in a DLQ |
| Message detail page repeats the preview | "Zoom": full-height body, tabs Body / Properties / Application / Raw, `[ ]` previous/next | No more back-and-forth to the list |
| Metrics: 2 step charts with overlapping lines, 12 lines | 4 small multiples (Incoming, Outgoing, Active, Dead-lettered) with block characters and a current value, 8 lines | Easier to read, 4 more list rows |
| No search across entities | `:` jump palette over cached listings: queues, subscriptions, actions, other namespaces | Fastest route to a DLQ |
| Retry confirm: a paragraph | Label grid (From / To / How / Note), primary button; progress and result in the same frame | Easier to scan before a move |

## Phase 1: chrome, tables, theme (no behaviour change)

- `UI/Palette.cs`: add `Chip`/`OnChip` (key chips), `Badge`/`OnBadge`, `LockedStrip`, `DeletedStrip`. Dark: `Chip = Color.Grey`(bright black), `OnChip = Color.White`; light: `Chip = Color.Silver`, `OnChip = Color.Black`. Still only the 16 palette colors.
- `Components/ConnectionHeader.razor`: drop the `Panel`; render `azsb │ crumb › crumb › current` and a right-aligned status (`● live · updated 4 s ago`, `◐ cached` while `IsRefreshing`), then a rule. `FullscreenLayout.Header` 3 → 2.
- All `SpectreTable`s on list pages: no column lines, a header rule only, no empty rows. Try `TableBorder.None` + a rule `Markup` first; if `SelectableList`/`NavigableTBody` need a border style that RazorConsole lacks, add the smallest border hook to the fork (`azsb`) with an upstream PR. Update `RoundedTableChrome`/`SimpleTableChrome`.
- `Pages/ConnectionPage.razor`: remove the `Figlet`; recompute its line budget.
- `UI/Icons.cs`: keep emoji only where they help (namespace icons on the connection page may stay); replace them on buttons, table cells and headers with the glyphs above. Update the comment and the CLAUDE.md theme paragraph. Test the glyphs in Windows Terminal, iTerm2 and GNOME Terminal: they must be one cell.
- Selection column: `[x]` / `[ ]` instead of ✅ / ⬜.
- Relative times in lists (`2 h ago`), absolute in the preview.

Tests: screenshots change; existing UI tests should pass unchanged.

## Phase 2: key map, `/` filter, help

- New `UI/KeyMap.cs`: `sealed record KeyBinding(string Key, string? Alias, string Label, bool Danger, Func<bool> Available, Action Run)`. A page declares its bindings once; the footer, the help overlay and `HandleKeyDown` all read them. Replaces the per-page `switch` and the hand-written footer buttons, which today repeat the same keys three times.
- New `Components/KeyHints.razor`: renders bindings as chips (`D Delete`), clickable like `KeyButton`, wrapping to a second line only if needed.
- Keys (full list on the Help mockup):
  - everywhere: `/` filter, `:` jump, `?` help, `⇥` next tab, `m` metrics (`+ -` zoom), `esc` back, `q` quit on the connection page
  - lists: `⏎` open, `s`/`S` sort, `^r` refresh, `D` delete, `P` purge DLQ, `1 2 3` sections
  - messages: `p` peek, `r` receive, `space` select, `^a` all, `i` invert, `R` retry/resend, `P` purge, `[ ]` previous/next, `y` copy
  - locked: `c` complete, `a` abandon, `X` dead-letter, `R` resend, `esc` abandon the rest and leave
  - aliases: every current F-key keeps working for at least one release
- `ColumnFilter`/`FilterEdit`: filter opens on `/`, closes on `esc` (clears) or `⏎` (keeps). The `-`/`_` metrics exception goes away.
- `?` opens `Components/HelpDialog.razor` built from the page's bindings plus the global ones.

Tests: tests that type into the filter send `/` first; add one test per page that each footer binding runs the same action as its F-key alias.

## Phase 3: namespace tabs

- Replace the section dropdown (`SectionCrumb`, `UI/Section.cs`) with a tab strip in the page header: `Queues 5  Topics 3  Subscriptions 7`. Routes stay as they are; a tab navigates. Hide Topics/Subscriptions without `IHasTopics`.
- Counts come from the cached listings, so the strip renders at once.
- Right side of the tab row: `Standard · West Europe · rg-messaging-dev`.

## Phase 4: message browser

- `MessageBrowserView.razor`: Active/Dead-letter as tabs (`⇥`), counts from the queue/subscription info. Mode badge `PEEK` on the right; in `BrowseMode.Locked` a full-width strip (yellow, `LOCKED · 22 held · locks renew automatically · esc abandons the rest`); after ReceiveAndDelete a red strip until requeued.
- Locked lists keep completed/abandoned rows (dimmed, with a State column) instead of removing them, so you see what you did; a `Lock` column shows the renewal.
- Single quick actions in Locked mode (complete, abandon, dead-letter) report in a one-line status (`✓ Completed 2 messages · 22 still held`) instead of `OperationDialog`. Retry, Resend, Requeue and Purge keep `OperationDialog`. This changes the CLAUDE.md rule "batch operations end in OperationDialog": update it.
- `MessagePreview.razor`: dead-letter line first (`✖ reason  description`), then two columns: properties (44 cols, grouped MESSAGE / APPLICATION) and body with line numbers and a `content-type · size` header.
- Abstractions: add `DeadLetterErrorDescription` to `MessageInfo`; map it in `ServiceBusMessageInfo` and the emulator, and drop it from `Properties` like the reason. Include it in `MessageMarkdown`/`MessageJson`.
- Footer labels carry the count they act on: `Retry 2`, `Complete 3`.

## Phase 5: message zoom

- Rework `MessageDetailPage.razor` into the zoom view: tabs Body / Properties / Application / Raw (`⇥`), full-height scrollable body, `w` wrap, `[ ]` previous/next through `MessageBrowser`'s list (the store already shares it), `y` copies the body with OSC 52, so it works over SSH.
- Optional: `e` writes the body to a temp file and opens `$EDITOR`.

## Phase 6: metrics

- `MetricsPanel.razor`/`UI/MetricsChart.cs`: four small multiples, each `title  value` + 5 rows of `▁▂▃▄▅▆▇█` + a time axis; title rule `── payments · queue · last 24 h ───── zoom - +`. `FullscreenLayout.MetricsPanel` 12 → 8. Keep the interval rule (finest grain at most 1.5 points per column).
- Optional, measure first: an "Incoming · 24 h" sparkline column on the list pages from one Azure Monitor call per namespace split by `EntityName`, polled much slower than counts (5 min) and cached. Skip it if it adds throttling.

## Phase 7: jump palette

- `Components/JumpDialog.razor` on `:`: fuzzy match over the current namespace's cached queues, topics and subscriptions (from `FileCache`), plus actions on the match (open dead-letter, purge, retry) and namespaces in other subscriptions (`^a` searches all namespaces). `⏎` opens, `⇥` opens the dead-letter queue.
- Results grouped by kind; show the dead-letter count so a DLQ with messages stands out.
- Pure function `JumpIndex.Search(string query, IEnumerable<JumpTarget>)` with unit tests; the dialog only renders it.

## Phase 8: connection page and dialogs

- `ConnectionTree.razor`: real columns (Namespace, SKU, Region, Resource group, Status, Opened) instead of a `·`-joined detail string; a RECENT group on top (last 5 opened namespaces, kept in the file cache); collapsed subscriptions show `3 namespaces`. Keys `⏎` queues, `t` topics, `s` subscriptions.
- One `Components/Dialog.razor` frame for `ConfirmModal`, `FetchDialog`, `OperationDialog`, `ErrorModal`: title in the border, a label grid, a primary button (`y Retry 2`) and a plain one (`n Cancel`). Purge asks you to type the entity name.

## Order and size

Phases 1 and 2 give most of the visible gain and unblock the rest; do them first, one PR each. 3, 4, 5 and 8 are independent after that. 6 and 7 are the most work and the most optional.

## Open questions

- Keep the namespace emoji on the connection page, or go glyph-only everywhere?
- `r` for Receive and `^r` for refresh: fine, or should Receive be on another key?
- Is a per-entity sparkline worth one extra Azure Monitor call per namespace every 5 minutes?
