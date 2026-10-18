# ObenseuerOS

Plugin for Obenseuer 0.5.08 (buildid 25969064), version 0.2.0, GUID `nomad.obenseuer.os`. It is built for both BepInEx 6 Unity.Mono and BepInEx 5 (5.4.23 or later), and in-game testing is still pending. One zip has `ObenseuerOS.dll` (BepInEx 6) and `ObenseuerOS.BepInEx5.dll` (BepInEx 5). Each loader loads only its own copy. If the log shows an error about the other copy, delete the file for the loader you do not use.
Requires BepInEx 6 Unity.Mono (be.755 or newer, be.788 recommended) or BepInEx 5 (5.4.23 or later), and the Obenseuer Modding Framework (ObenseuerCore.dll, plugin `obenseuer.moddingframework`) version 0.10.0 or newer, and its WebUi module (`ObenseuerCore.WebUi.dll`, plugin `obenseuer.moddingframework.webui`) version 0.10.0 or newer, with the public WebUi API (`ObenseuerCore.WebUi`, `ObenseuerCore.WebView`).
A mod that depends on the framework must use a hard `BepInDependency` on it, as this plugin does.

Adds a **Viewports 8** icon to every in-game computer desk. Opening it shows one modal WebView view over the game window, hosting the shell page in `web\index.html`. The framework owns the WebView2 shim, the view, the theme and the message pump. The OS does not load `ObxWebView.dll` itself.

Status: built for both loaders, in-game testing pending. The desk has these apps: Explorer, Notepad, Recycle Bin, Command Prompt, Control Panel, Stalburg Web, Stalburg Mail, Stal-Ex, Stalburg Bank, Stalburg Times, TOR-Turnip Chat, Shop, Minesweeper, Solitaire, PC Master Guard and TurvaPro 2000.

- Stalburg Web sites: Stalburg Mail, Stalburg Bank, Stalburg Times, Stal-Ex, Jobs, Shop, NaSpoBet, LOTTO by NaSpoBet, Stalburg Wiki, EYEbook, PC Master, TurvaPro 2000, FREE RM GENERATOR, Kolhola Warez and VOITTAJA!.
- Viruses and pop-ups (`src/Virus`): infections, money theft, and the two antivirus apps.

Shop delivery: Parcel (picked up at the parcel locker) or Mail (one cardboard box in the mailbox, whose items go into inventory only when they all fit).
## Stock market (Stal-Ex)

Stal-Ex is the OS stock market: shares, charts, dividends and short selling. Prices move with market news. Mods can read prices, trade and start market events through the mod API.

## Mod API

Mods can add apps (with their own pages), browser sites, news sources, chat rooms and shop listings. They can also read prices, trade and start market events, and write mail and files. The API version is 1.0.0.

## Build

Build both loaders from the repo folder: `dotnet build -c Release` (BepInEx 6, output `bin\ObenseuerOS.dll`) and `dotnet build -c Release -p:Loader=BepInEx5` (BepInEx 5, output `bin\be5\ObenseuerOS.dll`). The sample mod in `examples\HelloOs` builds against these outputs. It is not in the zip. `ObenseuerOS.csproj` (net472, C# 5, warnings as errors) needs `ObenseuerCore.dll` and `ObenseuerCore.WebUi.dll` from a framework build for the same loader, read from `CORE_BIN` (default `..\ObenseuerCore\bin`, or `bin\be5` for BepInEx 5, where the WebUi DLL is in `webui\`). The BepInEx 6 build needs the BepInEx 6 core DLLs (`BepInEx.Core.dll`, `BepInEx.Unity.Mono.dll`, `SemanticVersioning.dll`), read from `BEPINEX` (default the game's `BepInEx\core`). The BepInEx 5 build takes its BepInEx DLLs from `BepInEx.BaseLib` 5.4.20, restored from NuGet via `nuget.config`. Set either variable in the environment to override it. It also needs the game's Managed folder (including `Newtonsoft.Json.dll`), read from `OBENSEUER_DIR` or `-p:GameDir=...`.

The mod API self-test checks the mod id and file path scoping without the game: `dotnet run --project tools\OsApiSelfTest -c Release`. It prints one line per check and exits 0 only when all checks pass.

The wiki self-test is a separate project: `dotnet run --project tools\WikiSelfTest -c Release`. It compiles the `src\Wiki` sources it needs with `tools\WikiSelfTest\WikiSelfTestMain.cs`, prints one line per check and exits 0 only when all checks pass.

## Deploy layout

```
BepInEx\plugins\ObenseuerOS\ObenseuerOS.dll          BepInEx 6
BepInEx\plugins\ObenseuerOS\ObenseuerOS.BepInEx5.dll BepInEx 5
BepInEx\plugins\ObenseuerOS\web\               the shell page (index.html and assets)
```

Keep both DLLs in `BepInEx\plugins\ObenseuerOS\` as the zip has them. Each loader loads only its own copy. If the log shows an error about the other copy, delete the file for the loader you do not use.

The WebUi module ships `ObxWebView.dll` and `web\obx-theme.css` next to `ObenseuerCore.WebUi.dll`. The build does not copy anything. The WebView2 runtime must be installed on the machine.

## Config

`BepInEx\config\nomad.obenseuer.os.cfg`:

- `[Debug] SelfCheck = false`: runs the path and limit self-check at start and logs the result.
- `[Laptop] ExtraNames = ` (empty): extra object names to treat as laptops, comma separated, besides `laptop_001b` and `Laptop`.
- `[Debug] LogLookTarget = false`: logs each object the look ray hits (path, layer, usable layer, colliders, interactables) to find a laptop name.
- `[Market] Currency = RM`: the money the stock market trades in, `RM` or `OC`.
- `[Bank] WithdrawFeePercent = 2`: the fee taken from a cash order by post, as a percent of the amount (at least one unit). Values are clamped to 0 to 20; NaN or infinite values become 2.
- `[Wiki] Enabled = true`: allow the Stalburg Wiki to contact stalburg.net. Off keeps the wiki offline even after the player allows it.
- `[Wiki] CacheMaxMB = 128`: most space the saved wiki copy may use, in megabytes. The oldest pages are removed first.
- `[Wiki] MinIntervalMs = 1000`: least time between two requests to stalburg.net, in milliseconds. Values below 500 are raised to 500.
- `[Viruses] Enabled = true`: computer viruses, pop-ups and infections. Off stops new infections and cleans existing ones, restoring files; a bought antivirus keeps working.
- `[Viruses] MoneyTheft = true`: let viruses take small amounts of OC from the wallet and RM from a bank account, within daily and weekly caps.

The laptop screen dump and the pinned-bounds line are debug logs. They appear only when the framework's `[Debug] Verbose` is true.

The theme is not an OS setting. It is the WebUi module's `Appearance.WebTheme` (light, dark or system; default light), read through `WebUi.Theme`.

## Shell messages

Every message is one JSON object with `type`. Messages from C# carry `"v":1`. The shell should send `"v":1` too; C# does not check it.

- Shell to C#:
  - `{"type":"ready"}` after the page has loaded. C# sends the theme and app list only after this.
  - `{"type":"close"}` hides the OS and closes the desk app.
  - Escape (27) closes an open desktop menu, Start menu or notification panel first. In a mod app or site window it closes that window. Otherwise it sends `close`, unless focus is in an input, textarea or select, where Escape does nothing. F10 (121) always hides the OS: the framework reports it to the OS as a key event, so the shell does not send it.
- C# to shell:
  - `{"v":1,"type":"theme","mode":"light"}` (or `dark`), sent after `ready` and on every open. The framework also posts it to every view when the config changes.
  - `{"v":1,"type":"apps","apps":[{"id":"...","title":"..."}]}`, sent with the theme.

## Look

The shell is Viewports 8, the OS from INFRA (same universe): Win8-style windows with a translucent frame, a pale taskbar with the logo Start button, icon-only pinned and open apps (Explorer and Stalburg Web are pinned), Metro Start tiles and a boot splash. The logo is `web\brand\viewports.svg`. The default wallpaper is the in-game computer's own wallpaper, copied at runtime to `web\wallpapers\game.jpg` while you play. Nothing from the game ships with the mod, and the file is gitignored. Until the game sends it (for example in browser preview), it shows the plain colour. The other choices are the CSS `sewer` and `plain`. Colours are `--obx-os-*` tokens in `web\obx-theme.css`; fonts are Segoe UI. The shell's own art is original.

## Third-party

- Tabler Icons 3.48.0 (`@tabler/icons`), MIT licence. Copyright (c) 2020-2026 Paweł Kuna.
- The icons in `web\icons\` are Tabler's outline set, with `-filled` variants where they exist. The licence is `web\icons\LICENSE-tabler.txt`.
