# ObenseuerOS code notes

## `src/OsUguiPrototype.cs:OsUguiPrototype.Build`

`src/OsUguiPrototype.cs:OsUguiPrototype.Build` draws the desktop and an editable Notepad-like window through the modding framework's `DesktopUi` API. It is a visual and input experiment, not a replacement for the OS: its icon glyphs are placeholders, the text is not saved, and the other desktop icons are decorative. It uses the published web desktop's light-theme colors and proportions as an initial reference. Font rendering, shadows, blur, input behavior, scaling, and the final appearance need a real Unity comparison. This prototype requires the framework branch that introduces `DesktopUi`.

## `src/OsPlugin.cs:OsPlugin.Awake`

`src/OsPlugin.cs:OsPlugin.Awake` registers the separate computer app only when `Debug/UguiPrototype` is enabled. The existing OS remains available for side-by-side evaluation.

## `tools/ugui-reference.py:main`

`tools/ugui-reference.py:main` captures the existing web desktop and Notepad at 1280 by 720 in GitHub CI. The screenshots are reference images only: CI has no Unity player or game assets, so it cannot render the prototype or establish visual equivalence.
- `src/OsUguiPrototype.cs:OsUguiPrototype.Build` and `OsUguiPrototype.MakeWindow` explicitly select Unity UI buttons because the game's global button type has the same name; the desktop color expression uses the project's C# language version.
- `src/OsUguiPrototype.cs:OsUguiPrototype.Icon` loads rasterized copies of the OS icon artwork embedded by CI. The sprites are cached for the session; a missing icon leaves its tile blank.
- `src/OsUguiPrototype.cs:OsUguiPrototype.Build` uses the OS desktop icon sizes, ordering, colors, and dark taskbar as an initial visual match. It remains an incomplete prototype, not an implementation of the other apps.
- `src/OsUguiPrototype.cs:PrototypeStatus.Update` reads the framework's game clock and wallet for the prototype taskbar while it is visible.
- `src/OsUguiPrototype.cs:OsUguiPrototype.Build` resolves canvas layout before reading host and desktop sizes; the game trace showed an unsized host caused a one-pixel Notepad window and a single row of icons.
