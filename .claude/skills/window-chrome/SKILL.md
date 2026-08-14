---
name: window-chrome
description: How FreeWilly's WPF windows are formatted. Use whenever touching MainWindow, LogWindow, XAML, styles, colours, fonts, or anything a user sees — and before adding a new window, page, row or chip.
---

# Window chrome — claude-tray is the reference

**`d:\Git\alegauss\claude-tray` is this project's reference for interface formatting.**
Before deciding how something in the window should look or be structured, look at how
claude-tray does it and do it the same way. Two apps by the same author that look
unrelated read as two unfinished apps.

Its source lives in `src/Ui/`. The files worth reading first are `Brand.cs`,
`MainWindow.xaml` and the pages beside it.

## What has already been borrowed, and how

**`Brand.cs` → `src/FreeWilly.Tray/Ui/Palette.cs` (DD34).** A colour whose meaning is
not a free choice is declared **once, as bytes**, and each edge converts:

- GDI+ (`System.Drawing.Color`) for the tray icon
- a **frozen** `SolidColorBrush` for WPF
- a hex string for anything textual

Markup reaches it with `{x:Static ui:Palette.Danger}` — never a `#RRGGBB` literal. A test
(`PaletteTests`) fails the build on any hex colour in any `.xaml`. The font stack is in
`Palette` too, for the same reason and reached the same way.

Freeze every shared brush. They cross windows and are never mutated, and an unfrozen one
pays a lock on every draw.

## Three things that measurably do not work

Found by capturing the window and comparing, not by reasoning. Do not undo them:

1. **`ThemeMode` stays on each `Window` in markup.** Setting it on the `Application`, or
   from code before or after `InitializeComponent`, renders differently — wider control
   metrics, different button chrome.
2. **A `BasedOn="{StaticResource {x:Type Button}}"` style stays in the window that uses
   it.** `ThemeMode` puts Fluent in the *window's* resources, so the same `BasedOn`
   resolved at application scope finds the pre-Fluent `Button` and silently reintroduces
   the fallback it exists to prevent.
3. **An implicit `Style TargetType="Window"` does not reach a `Window` subclass.**
   Implicit styles are keyed by the exact type. It is ignored, silently.

`Ui/Theme.xaml` therefore holds only what has no dependency on the theme, and
`Ui/Theme.cs` is the one place a `System.Windows.Application` is constructed.

## Always capture before and after

`FreeWilly.exe --capture-window <png> [containers|images|volumes]` renders the window
off-screen and needs no desktop. A change that is meant to be invisible must produce a
**byte-identical** PNG:

```
dotnet publish src/FreeWilly.Tray -c Release
FreeWilly.exe --capture-window before.png containers
# ...change...
FreeWilly.exe --capture-window after.png containers
cmp before.png after.png
```

The capture is deterministic — verified by re-capturing unchanged code — so a difference
is a real difference. Every one of the three findings above came from this and from
nothing else; the test suite saw none of them.

## Where the rest is written down

`docs/specs/DD34-window-constitution.md` carries the eight laws the window is judged by
and the order the remaining tasks land in. Read L1 before adding any value, and L2 before
adding any window.
