# ObenseuerOS

A working computer for every desk in Obenseuer. Click the Viewports 8 icon on any computer and you get a desktop with windows, a taskbar and Start menu, a browser for the websites of Stalburg, and a set of apps: mail, bank, shop, stock market, jobs, betting, chat and games. It also has viruses.

It is a BepInEx plugin built on the Obenseuer Modding Framework, and other mods can add their own apps and websites to it.

## Features

- **Apps:** Explorer, Notepad, Recycle Bin, Command Prompt, Control Panel, Stalburg Web, Stalburg Mail, Stalburg Bank, Stal-Ex (stock market), Stalburg Times, TOR-Turnip Chat, Shop, Minesweeper, Solitaire, PC Master Guard and TurvaPro 2000.
- **Websites:** Stalburg Mail, Bank, Times, Stal-Ex, Jobs and Shop, NaSpoBet (sports betting), LOTTO, the Stalburg Wiki, EYEbook, PC Master, TurvaPro 2000, and a few sites you should not trust.
- **Stock market:** shares, charts, dividends and short selling. News moves the prices.
- **Shop:** orders arrive at the parcel locker or as a box in your mailbox. The items go into your inventory only when they all fit.
- **Viruses:** bad downloads bring pop-ups, fake errors and small thefts from your accounts. Two antivirus programs can clean them up.

## Requirements

- Obenseuer 0.5.08
- BepInEx 6 (Unity.Mono, be.755 or newer, be.788 recommended) or BepInEx 5 (5.4.23 or newer)
- Obenseuer Modding Framework 0.10.0 or newer, with its WebUi module
- The Microsoft WebView2 runtime

## Install

Copy the `BepInEx` folder from the zip into the game folder. You should end up with:

```
BepInEx\plugins\ObenseuerOS\ObenseuerOS.dll           for BepInEx 6
BepInEx\plugins\ObenseuerOS\ObenseuerOS.BepInEx5.dll  for BepInEx 5
BepInEx\plugins\ObenseuerOS\web\                      the desktop pages
```

Keep both DLLs. Each version of BepInEx only loads its own. If the log complains about the other one, delete the DLL you are not using.

## Config

The file is `BepInEx\config\nomad.obenseuer.os.cfg`.

| Setting | Default | What it does |
| --- | --- | --- |
| `[Market] Currency` | `RM` | Money the stock market trades in, `RM` or `OC`. |
| `[Bank] WithdrawFeePercent` | `2` | Fee on a cash order by post, in percent (0 to 20). |
| `[Viruses] Enabled` | `true` | Turns viruses and pop-ups on or off. Off stops new infections and cleans existing ones. |
| `[Viruses] MoneyTheft` | `true` | Lets viruses take small amounts of money, within daily and weekly caps. |
| `[Wiki] Enabled` | `true` | Lets the Stalburg Wiki contact stalburg.net. |
| `[Wiki] CacheMaxMB` | `128` | Most space the saved wiki copy may use. |
| `[Wiki] MinIntervalMs` | `1000` | Shortest time between two wiki requests (500 at least). |
| `[Laptop] ExtraNames` | empty | Extra object names to treat as computers, comma separated. |
| `[Debug] SelfCheck` | `false` | Runs a small self-check at start and logs the result. |
| `[Debug] LogLookTarget` | `false` | Logs what the look ray hits, to find a computer's object name. |

The light or dark theme comes from the WebUi module's `Appearance.WebTheme` setting.

## For modders

Mods can add apps (with their own pages), browser sites, news sources, chat rooms and shop listings. They can also read prices, trade shares, start market events, and write mail and files. The API version is 1.0.0.

- `examples/HelloOs` is a small working app to copy.
- `web/sdk/PROTOCOL.md` explains how an app page talks to the OS, and `web/sdk/os-app.js` is the helper script.
- A mod that depends on this one should use a hard `BepInDependency` on `nomad.obenseuer.os`.

## Build

```
dotnet build -c Release                          BepInEx 6, output bin\ObenseuerOS.dll
dotnet build -c Release -p:Loader=BepInEx5       BepInEx 5, output bin\be5\ObenseuerOS.dll
```

The project is `net472`, C# 5, with warnings as errors. It needs these, which you can point at with environment variables:

- `CORE_BIN`: a build of `ObenseuerCore.dll` and `ObenseuerCore.WebUi.dll` for the same loader (default `..\ObenseuerCore\bin`, or `bin\be5` for BepInEx 5, where the WebUi DLL is in `webui\`).
- `BEPINEX`: the BepInEx 6 core DLLs (default the game's `BepInEx\core`). The BepInEx 5 build restores its DLLs from NuGet.
- `OBENSEUER_DIR` (or `-p:GameDir=...`): the game folder, for its Managed DLLs.

The sample mod in `examples\HelloOs` builds against these outputs.

## Credits

- Icons are from Tabler Icons 3.48.0 (MIT licence, copyright 2020-2026 Paweł Kuna). The licence is in `web\icons\LICENSE-tabler.txt`.
- Stalburg and its people belong to Obenseuer and its creators. The desktop art is original.
