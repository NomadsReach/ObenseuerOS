# ObenseuerOS code notes

## `src/OsUguiPrototype.cs:OsUguiPrototype.Build`

The opt-in prototype draws the desktop and an editable Notepad-like window with the game's uGUI components. It is a visual and input experiment, not a replacement for the OS: its icon glyphs are placeholders, the text is not saved, and the other desktop icons are decorative. It uses the published web desktop's light-theme colors and proportions as an initial reference. Font rendering, shadows, blur, input behavior, scaling, and the final appearance need a real Unity comparison.

## `src/OsPlugin.cs:OsPlugin.Awake`

The separate computer app is registered only when `Debug/UguiPrototype` is enabled. The existing OS remains available for side-by-side evaluation.

## `tools/ugui-reference.py:main`

GitHub CI captures the existing web desktop and Notepad at 1280 by 720. The screenshots are reference images only: CI has no Unity player or game assets, so it cannot render the prototype or establish visual equivalence.
