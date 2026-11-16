# ObenseuerOS code notes

## `src/OsUguiPrototype.cs:OsUguiPrototype.Build`

`src/OsUguiPrototype.cs:OsUguiPrototype.Build` draws the desktop and opens the native Notepad prototype and Minesweeper window through the modding framework's `DesktopUi` API. It is not a replacement for the OS: Notepad text is not saved and the remaining desktop apps are not yet ported. Font rendering, shadows, blur, input behavior, scaling, and final appearance need a real Unity comparison. This prototype requires the framework branch that introduces `DesktopUi`.

## `src/OsPlugin.cs:OsPlugin.Awake`

`src/OsPlugin.cs:OsPlugin.Awake` registers the separate computer app only when `Debug/UguiPrototype` is enabled. The existing OS remains available for side-by-side evaluation.

## `tools/ugui-reference.py:main`

`tools/ugui-reference.py:main` captures the existing web desktop and Notepad at 1280 by 720 in GitHub CI. The screenshots are reference images only: CI has no Unity player or game assets, so it cannot render the prototype or establish visual equivalence.
- `src/OsUguiPrototype.cs:OsUguiPrototype.MakeWindow` explicitly selects the Unity UI button because the game's global button type has the same name.
- `src/OsUguiPrototype.cs:OsUguiPrototype.Icon` loads rasterized copies of the OS icon artwork embedded by CI. The sprites are cached for the session; a missing icon leaves its tile blank.
- `src/OsUguiPrototype.cs:OsUguiPrototype.Build` uses the OS desktop icon sizes, ordering, colors, and dark taskbar as an initial visual match. It remains an incomplete prototype, not an implementation of the other apps.
- `src/OsUguiPrototype.cs:PrototypeStatus.Update` reads the framework's game clock and wallet for the prototype taskbar while it is visible.
- `src/OsUguiPrototype.cs:OsUguiPrototype.Build` resolves canvas layout before reading host and desktop sizes; the game trace showed an unsized host caused a one-pixel Notepad window and a single row of icons.
- `src/OsUguiPrototype.cs:OsUguiPrototype.Build` registers desktop tiles with the framework's selectable control; Notepad and Minesweeper open on double-click while unported apps remain inactive.
- `src/OsUguiPrototype.cs:OsUguiPrototype.MakeWindow` uses the original close icon and aligns the close button with the right edge of the window title bar.
- `src/Ugui/MinesweeperGame.cs:MinesweeperGame` ports the existing Minesweeper board rules, including the safe first reveal, flags, question marks, flood reveal, chording, and win/loss state. Randomness is injected so CI can exercise deterministic boards.
- `src/Ugui/MinesweeperWindow.cs:MinesweeperWindow` presents the native board, difficulty controls, timer, and pointer interactions. Difficulty is stored separately from game saves as a Unity preference.
