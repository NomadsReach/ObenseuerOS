# ObenseuerOS code notes

## `src/OsUguiPrototype.cs:OsUguiPrototype.Build`

`src/OsUguiPrototype.cs:OsUguiPrototype.Build` draws the desktop and opens the native Notepad prototype and Minesweeper window through the modding framework's `DesktopUi` API. It is not a replacement for the OS: the remaining desktop apps are not yet ported. Font rendering, shadows, blur, input behavior, scaling, and final appearance need a real Unity comparison. This prototype requires the framework branch that introduces `DesktopUi`.

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
- `src/Ugui/NotepadWindow.cs:NotepadWindow` offers New, Open, Save, Save As, Select All, and word wrap, and asks for confirmation before discarding changes or overwriting a document. `src/OsNotepadStorage.cs:OsNotepadStorage` connects it to the OS's per-save disk and write guard. The standalone F9 prototype does not own that disk and reports that saving is unavailable. Closing the whole prototype or changing scenes can still discard unsaved text; those host-level close paths need integration before migration.
- `src/ToolkitPrototype.cs:ToolkitPrototype.Open` uses the framework's UI Toolkit overlay for a separate experimental desktop. `src/ToolkitNotepad.cs:ToolkitNotepad` connects the same OS storage to native Notepad controls when hosted by `src/OsPlugin.cs:OsPlugin.Awake`; the standalone F10 build has no storage. The framework close guard protects standalone close requests, but leaving a computer desk or changing scenes can still discard unsaved text. The interface needs in-game visual and input verification.
- `ObenseuerOS.csproj:EmbeddedResource` embeds rasterized icons when they are prepared from the SVG assets. The public build pipeline does not yet generate them, and a plain build without that preparation has no desktop icon resources.
- `src/OsNotepadStorage.cs:OsNotepadStorage.CheckSession` binds each editor to the current load generation and refuses file access while the game saves or loads. The editor must be reopened after load, new game, or return to the main menu so an old document cannot write into another session.
- `.github/workflows/ugui-reference.yml:rules` runs the Minesweeper model and Notepad storage-adapter checks on GitHub's public OS repository. The adapter check uses fake game services; neither this job nor the reference screenshots compile the game integration or exercise Unity input.
