# DD34 — The window — design laws, and what claude-tray already settled

> Roadmap: [ROADMAP.md](../ROADMAP.md) Block **C** ·
> Design: [IMPROVEMENTS.md](../IMPROVEMENTS.md) §DD34–§DD39
> Status: 📋 designed, not started · deps: none — DD34 is the floor the rest stand on
> Scope: this document is the **constitution for the human surface** — the laws a
> window is judged by, the elements they imply, and the decomposition into DD34–DD39.
> [DD23](DD23-agent-first-dockerdesk.md) is its counterpart for the agent surface.
> Each task keeps its own rationale in `IMPROVEMENTS.md`; this is the premise under
> all of them.

---

## 1. Where this comes from

`MainWindow.xaml`'s first comment already states the intent:

> The chrome borrows claude-tray's: `ThemeMode="System"` so light and dark follow the
> OS, the Fluent brushes by `DynamicResource`, and Segoe UI Variable — two apps by the
> same author that look unrelated read as two unfinished apps.

That is the right instinct and it was applied at the level of *one window's chrome*.
claude-tray is not a set of chrome choices, though; it is a small design system with a
shell, pages, cards, chips, rows, a declared palette, and a way to render any of it
without the live data behind it. This document is that system restated for DockerDesk,
so the borrowing is structural rather than cosmetic.

**What is already right and is not up for revision.** The engine dot and its word. The
three designed empty states, which distinguish *nothing here* from *nothing running*.
The engine's own refusal printed under the row that caused it, never in a dialog. The
pending word that owns the half-second between a click and the daemon's first answer.
`Shell` disabled with a tooltip rather than hidden, because *not yet* and *not at all*
are different answers. The header-columns-match-the-rows tests. Every law below is
written to protect these, not to trade them for a nicer surface.

---

## 2. The laws

### L1 — A value with one meaning has one declaration

`#E5484D` means *the engine refused, or this is stderr*. It is written four times
across two files and pinned by nothing. The three engine-state colours have a second,
separate home in `StateIcon.ColourFor`, in GDI+, converted to a WPF brush by hand at
one call site.

claude-tray's `Brand.cs` answers exactly this, and its docstring carries the reasoning:
share the **value**, as bytes, and let each edge convert — GDI+ for the tray icon, a
frozen `Brush` for WPF, a hex string for anything textual. No one type serves all four
consumers, so the bytes are what is shared.

Applies to: colours, the font stack, `ThemeMode`, corner radii, the type scale, and
every style currently declared twice. → **DD34**

### L2 — The shell owns the chrome; a list owns its page

One window class currently carries the engine banner, three lists, three header rows,
three empty states, two prune confirmations, the log windows and the terminal launch:
447 lines of XAML, 586 of code-behind. The three lists are three hand-written copies of
one stanza, and DD12 plus networks add a fourth and a fifth.

claude-tray's shell is 104 lines of XAML and owns only a nav strip and a
`DestinationHost`. Each destination is a `UserControl` with its own header and footer,
built on first navigation and then kept alive collapsed — so state survives switching
away and a destination nobody opens is never built. Heavy pages split again by concern
across partial classes. → **DD35**

### L3 — Anything scanned down a column is drawn to be scanned

State is the column the eye actually runs down, and it is drawn with the least: plain
tertiary text beside a `Status` that restates the same fact in the daemon's words.
claude-tray puts that class of fact in a tinted chip — rounded `Border`, translucent
tint that works on both surfaces, tooltip carrying the evidence, because a chip is an
assertion and an assertion travels with its inputs. → **DD36**

### L4 — A row is a surface, not a form

Six word-captioned buttons per row, in a fixed 320px column, is two hundred captions on
a forty-container machine. Rows do not highlight on hover, so nothing says a row is a
row. The verbs are pressed once a session; the row is read constantly. Keep visible
what a row is opened for, move the rest behind hover or a context menu — the pattern
claude-tray's `SourceRowTemplate` already uses, negative margins and all, so the
highlight bleeds past the text while the columns stay aligned to the pixel. → **DD36**

### L5 — A long list is narrowed and reordered, never scrolled

Every heading here is a dead `TextBlock`. claude-tray templates a sorting heading as a
`Button` whose template is a `TextBlock` — it reads as a label and behaves as a
control — and its comment gives the reason: *a heading that reorders on click and gives
no affordance is a feature only its author finds*. Sorting is over rows in hand.
Filtering likewise: never a second call to the daemon, and it must survive a refresh
arriving from the event stream. → **DD37**

### L6 — Any window can be drawn without the thing it is about

Every window takes a `DockerApi`, so nothing can be looked at without a daemon holding
the right containers. Reviewing a UI change means describing it; a screenshot is
whatever the machine was running that afternoon, which is also somebody's container
names in a public README; and the designed empty states are the hardest of all to reach
deliberately.

claude-tray's answer is fixtures plus flags: a known machine built in code, a flag per
page, and `PageWindow` — a bare host so a preview is the page and not the shell around
it. The captures are deterministic, which is the whole reason they are reviewable, and
it is what gives **DD22**'s off-screen render something to photograph. → **DD38**

### L7 — The window remembers what the user set on purpose

Fixed size, `CenterScreen`, nothing persisted. Placement and the last destination are a
handful of values and there is already an `ArtefactStore`. Two rules, because both are
how this is usually got wrong: restore a rectangle only onto a monitor that still
exists, and remember *maximised plus restore bounds*, never a screen-sized rectangle.
→ **DD39**

### L8 — Shape before colour, and the engine's words before ours

Already law in this repo, restated so a redesign cannot quietly drop it.
`StateIcon`'s docstring: shape carries the state, colour only reinforces it, and
`InkedPixels` makes that testable. Every chip L3 introduces carries a word, never a
colour alone. Every failure is the daemon's sentence, passed through.

---

## 3. The elements this implies

| Element | Today | After |
|---|---|---|
| Palette | 4× `#E5484D`, GDI+ colours converted by hand | one `Palette` of values, one `Theme.xaml` |
| Shell | one `Window` with a `TabControl` | shell + one page per list, built lazily, kept alive |
| Card | none — flat lists on transparent | `GroupCard`: 8px radius, card fill, card stroke |
| Row | plain cells, no hover, 6 buttons | hover surface, chip, primary verb + overflow |
| Chip | none | tinted `Border` + word + evidence tooltip |
| Heading | dead `TextBlock` | `Button` templated as a label, sorts, glyph |
| Filter | none | one box per list, over rows in hand |
| Empty | 3 designed states ✅ | 4 — the filter that matched nothing |
| Preview | none | fixture + a flag per page + a bare host |
| Persistence | none | placement, destination, sort, filter |

---

## 4. Order, and what each task may not break

```
DD34 (palette + theme)  ──┬── DD35 (shell + pages) ──┬── DD37 (sort + filter)
                          │                          └── DD38 (fixtures + preview) ── DD22
                          └── DD36 (rows + chips)
DD39 (persistence)  — independent
```

DD34 first because everything else adds styles, and adding them before there is a place
to put them is how the fourth `#E5484D` got written. DD38 is worth pulling forward the
moment DD35 lands: after it, every later task is reviewable as a picture instead of a
description, and DD22 stops needing the live screen.

Three things every one of these must still be true of when it is done:

1. The empty states still say which of the two reasons a list is empty applies.
2. A refusal still appears under the row that caused it, in the daemon's own words.
3. The header columns still match the row columns, and the test still says so.

---

## 5. Deliberately not here

- **Localisation.** claude-tray has `{local:Loc}` and `lang/`. DockerDesk's strings are
  English in markup and code-behind. That is a scope decision to take on its own
  evidence — a second locale actually wanted — not a side effect of a visual pass.
- **An icon font pass.** claude-tray leans on Segoe MDL2 Assets. Worth having, but it
  is L4's overflow menu that makes it necessary, so it rides with DD36 rather than
  claiming a task.
- **A theme toggle.** `ThemeMode="System"` is the answer. A third setting to maintain
  buys nothing over the OS switch the user already has.
- **A dashboard.** Nothing here aggregates. The window answers *what is running* and
  *what is on disk*; charts belong to the app that measures usage, not to this one.
