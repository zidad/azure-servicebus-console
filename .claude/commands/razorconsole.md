# RazorConsole reference for this project

RazorConsole renders Blazor Razor components to the terminal via Spectre.Console. There is no CSS, no browser DOM, and no HTML rendering — every component maps to a Spectre.Console `IRenderable`. All components live in the `RazorConsole.Components` namespace, which is globally imported in `Pages/_Imports.razor`.

---

## Rendering model

- The terminal is redrawn on every `StateHasChanged()` call (full re-render).
- Layout is driven by Spectre.Console's compositor: `Rows` stacks vertically, `Columns` lays out horizontally, `Panel` wraps in a box, `Padder` adds whitespace.
- There is no asynchronous partial rendering — call `await InvokeAsync(StateHasChanged)` from background tasks to marshal back to the Blazor dispatcher.
- Keyboard events bubble through the virtual DOM the same way they do in browser Blazor. Attach `@onkeydown` to a `<Rows>` root to catch all events from the page; also pass `OnKeyDown="@HandleKeyDown"` to individual `TextButton` elements (they fire the callback before the event bubbles).

---

## Component catalogue

### Layout

| Component | Key parameters | Notes |
|---|---|---|
| `<Rows>` | `Expand`, `@onkeydown`, `@attributes` | Vertical stack. Use as the root element of every page with `@onkeydown="HandleKeyDown"`. |
| `<Columns>` | `Expand` | Horizontal layout. Use for button bars and side-by-side content. |
| `<Panel>` | `Title`, `BorderColor`, `Border` (BoxBorder), `Padding` (Padding struct), `Expand`, `Height`, `Width` | Titled bordered box. `Padding="@(new(left, top, right, bottom))"`. |
| `<Padder>` | `Padding` (Padding struct) | Adds whitespace around content. `Padding="@(new(0, 1, 0, 0))"` adds one blank line above. |
| `<Newline>` | — | Inserts a blank line. |

### Text

| Component | Key parameters | Notes |
|---|---|---|
| `<Markup>` | `Content`, `Foreground` (Color), `Background` (Color), `Decoration` (Decoration) | Renders a Spectre.Console markup string. **Always escape square brackets**: `text.Replace("[", "[[").Replace("]", "]]")`. |
| `<Figlet>` | `Content`, `Color`, `Justify` | Large ASCII-art title text. |
| `<SyntaxHighlighter>` | `Code`, `Language` | Syntax-highlighted code block. |
| `<Markdown>` | `Content` | Renders Markdown. |

### Input

| Component | Key parameters | Notes |
|---|---|---|
| `<TextButton>` | `Content`, `FocusedColor`, `BackgroundColor`, `FocusOrder`, `OnClick` (EventCallback), `OnFocus` (Action), `OnBlur` (Action), `OnKeyDown` (Action) | Clickable label. `FocusKey` property exposes the internal GUID for programmatic focus. |
| `<TextInput>` | `Value`, `ValueChanged`, `Placeholder`, `Label`, `Expand`, `FocusOrder`, `OnFocus`, `OnBlur`, `OnInput`, `OnSubmit` | Text field. Use `ValueChanged` for reactive filtering. |
| `<Select<TItem>>` | `Options` (TItem[]), `Value`, `ValueChanged`, `FocusedValue`, `FocusedValueChanged`, `Expand`, `FocusOrder`, `SelectedOptionForeground`, `Formatter`, `Comparer` | Keyboard-navigable pick list. Arrow keys move highlight; Enter commits; Escape cancels; typing ahead jumps to matching option. |

### Tables

```razor
<SpectreTable Expand="true" Border="TableBorder.Rounded">
    <SpectreTHead>
        <SpectreTR>
            <SpectreTH Align="Justify.Left">Name</SpectreTH>
            <SpectreTH Align="Justify.Right">Count</SpectreTH>
        </SpectreTR>
    </SpectreTHead>
    <SpectreTBody>
        @foreach (var item in items)
        {
            <SpectreTR @key="item.Id">
                <SpectreTD><TextButton Content="@item.Name" .../></SpectreTD>
                <SpectreTD Align="Justify.Right">@item.Count</SpectreTD>
            </SpectreTR>
        }
    </SpectreTBody>
</SpectreTable>
```

`SpectreTable`: `Expand`, `Border` (TableBorder), `Title`, `ShowHeaders`, `BorderColor`.
`SpectreTH` / `SpectreTD`: `Align` (Justify).
Always add `@key` on `SpectreTR` to prevent render-tree diff crashes on list updates.

### Scrolling

**`<Scrollable<TItem>>`** — paginates a list, exposes only the visible slice to the render fragment.

```razor
<Scrollable Items="@_list" PageSize="@(Math.Max(5, Console.WindowHeight - 12))" Scrollbar="new()">
    <SpectreTable ...>
        <SpectreTBody>
            @foreach (var item in context.Items)   // only the visible page
            {
                <SpectreTR @key="item.Id">...</SpectreTR>
            }
        </SpectreTBody>
    </SpectreTable>
</Scrollable>
```

Parameters: `Items` (IReadOnlyList\<T\>), `PageSize`, `ScrollOffset` / `ScrollOffsetChanged`, `Scrollbar` (ScrollbarSettings?), `IsScrollbarEmbedded` (default true).
`context` is `ScrollContext<T>` with `.Items`, `.CurrentOffset`, `.PagesCount`, `.KeyDownEventHandler`.
When `Scrollbar="new()"` the scrollbar is embedded in the table/panel border and handles ArrowUp/Down, PageUp/Down, Home/End itself.

**`<ViewHeightScrollable>`** — clips rendered content to N lines and scrolls by line offset. Use for long text bodies (e.g. message JSON).

```razor
<ViewHeightScrollable LinesToRender="@(Math.Max(5, Console.WindowHeight / 2))" Scrollbar="new()">
    <Markup Content="@body" />
</ViewHeightScrollable>
```

Parameters: `LinesToRender`, `ScrollOffset` / `ScrollOffsetChanged`, `Scrollbar`, `IsScrollbarEmbedded`.
`ChildContent` is `RenderFragment<ScrollContext>` — the `context` variable is available but unused when the scrollbar handles navigation.

### Overlays

**`<ModalWindow>`** — renders content as a centered overlay over the terminal output.

```razor
<ModalWindow IsOpened="@(_isOpen)">
    <Panel Title="..." BorderColor="@Color.Red" Padding="@(new(2,1,2,1))">
        ...
    </Panel>
</ModalWindow>
```

The modal is rendered outside the normal flow; all page elements remain in the focus list while the modal is open. Use `FocusManager.FocusAsync(button.FocusKey)` in `OnAfterRenderAsync` to steal focus to a specific button when the modal opens (see `Components/ConfirmModal.razor`).

### Visual feedback

| Component | Key parameters | Notes |
|---|---|---|
| `<Spinner>` | `SpinnerType` (Spinner.Known.*), `Message`, `Style`, `AutoDismiss` | Animated spinner. Default is Dots. Use inside `SpectreTD` or `Columns` for loading indicators. |

---

## Focus system

- `FocusOrder` (int?) on `TextButton`, `TextInput`, `Select` determines tab order — lower = focused first.
- Elements without `FocusOrder` come last in tab order.
- `FocusManager` (singleton, injectable) manages focus programmatically:
  - `FocusAsync(string key)` — focus a specific element by its key.
  - `FocusNextAsync()` / `FocusPreviousAsync()` — move focus one step.
- `TextButton.FocusKey` (string) — the element's unique focus key, usable with `FocusAsync`.
- Use `@ref="_myButton"` on a `TextButton` to capture the instance, then call `FocusManager.FocusAsync(_myButton.FocusKey)`.

### Focus order conventions in this project

| FocusOrder | Role |
|---|---|
| 1 | Primary navigation button (e.g. `[Esc] Back`) |
| 2–5 | Action buttons (Refresh, Receive, DLQ toggle, Delete, Requeue) |
| 6+ | Table row items (assigned as `6 + i++` in the foreach) |

---

## Page template

Every page follows this pattern:

```razor
@page "/my-route/{Param}"
@namespace ServiceBusConsole.Pages
@implements IDisposable      // when there's a CancellationTokenSource

@inject NavigationManager Nav
@inject ServiceBusConnection Connection
@inject ISomeService SomeService
@inject NavigationState NavState   // when navigating to/from subscription-browser

<Rows @onkeydown="HandleKeyDown">
    {{header panel}}
    {{loading spinner or table}}
    <Padder Padding="@(new(1, 0, 0, 0))">
        <Columns>
            <TextButton Content="[F5] Refresh" OnClick="Load" FocusedColor="@Color.Green" FocusOrder="2" OnKeyDown="@HandleKeyDown" />
            <Markup Content="   " />
            <TextButton Content="[Esc] Back" OnClick="@(() => Nav.NavigateTo("/prev"))" FocusedColor="@Color.Yellow" FocusOrder="1" OnKeyDown="@HandleKeyDown" />
        </Columns>
    </Padder>
</Rows>

<ConfirmModal ... />
<ErrorModal ... />

@code {
    [Parameter] public string Param { get; set; } = "";

    private CancellationTokenSource _cts = new();
    private string _error = string.Empty;

    public void Dispose() { _cts.Cancel(); _cts.Dispose(); }

    protected override void OnInitialized()
    {
        if (Connection.CurrentNamespace is null) Nav.NavigateTo("/");
    }

    protected override async Task OnInitializedAsync() => await Load();

    private void HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.CtrlKey || e.AltKey || e.MetaKey) return;
        switch (e.Key)
        {
            case "Escape": Nav.NavigateTo("/prev"); return;
            case "F5": _ = Load(); return;
        }
    }
}
```

---

## Navigation patterns

### `NavigationState` (singleton)

```csharp
public class NavigationState
{
    public MessageInfo? Message { get; set; }
    public MessageSource? MessageSource { get; set; }
    public string? TopicName { get; set; }
    public string? SubscriptionName { get; set; }
    public string? ReturnPath { get; set; }   // set before navigating to /subscription-browser
}
```

### ReturnPath convention

Always set `NavState.ReturnPath` before navigating to `/subscription-browser`:

```csharp
// from SubscriptionListPage:
NavState.ReturnPath = $"/topics/{Uri.EscapeDataString(TopicName)}";

// from AllSubscriptionsPage:
NavState.ReturnPath = "/subscriptions";
```

Consume in `SubscriptionBrowserPage`:

```csharp
private string BackPath => NavState.ReturnPath
    ?? (string.IsNullOrEmpty(TopicName) ? "/topics" : $"/topics/{Uri.EscapeDataString(TopicName)}");
```

### Returning from MessageDetailPage

`MessageDetailPage.GoBack()` navigates back to either `/queues/{name}` or `/subscription-browser` based on `NavState.MessageSource.IsSubscription`. `ReturnPath` is preserved through the message detail trip automatically.

---

## Built-in modals

### `<ConfirmModal>`

```razor
<ConfirmModal IsOpened="@(_pendingDelete is not null)"
              Message="@($"Delete '{_pendingDelete}'?")"
              OnConfirm="ConfirmDelete"
              OnCancel="@(() => _pendingDelete = null)" />
```

Auto-focuses the Cancel button when opened. Always check for an open modal before navigating on Escape:

```csharp
case "Escape":
    if (_pendingDelete is not null) { _pendingDelete = null; StateHasChanged(); return; }
    Nav.NavigateTo(BackPath); return;
```

### `<ErrorModal>`

```razor
<ErrorModal Message="@_error" OnDismiss="@(() => _error = string.Empty)" />
```

---

## Key constraints

- **No CSS** — styling is Spectre.Console `Color`, `Decoration`, `Style` only.
- **`StateHasChanged()` = full redraw** — avoid calling it in tight loops.
- **Background tasks** — always use `await InvokeAsync(StateHasChanged)` from `async foreach` loops; the Blazor dispatcher is single-threaded.
- **Markup escaping** — square brackets are Spectre.Console markup syntax. Any user-supplied string rendered in `<Markup>` must have `[` → `[[` and `]` → `]]`.
- **`@key` on table rows** — always add `@key` on `<SpectreTR>` when iterating; omitting it causes render-tree diff crashes when the list is mutated.
- **`Scrollable` wraps the table, not the body** — the `<Scrollable>` tag goes outside `<SpectreTable>`, with `</Scrollable>` after `</SpectreTable>`.
- **`Spinner` inside `SpectreTD`** — works for per-cell loading indicators; use in place of `"…"` strings when `IsRefreshing` is true.
