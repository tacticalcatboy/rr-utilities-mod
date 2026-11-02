# RR Utilities (Railroader 0.6 port)

Utilities mod for [Railroader](https://store.steampowered.com/app/1683150/), originally by Vanguard
([Nexus](https://www.nexusmods.com/railroader/mods/19/)). This branch ports it from Unity Mod Manager
to the native mod loader added in Railroader 0.6 ("Maintenance of Way", Unity 6000.3).

Version 2.x targets Railroader **0.6.0 and later**. Use 1.2.x on 2025.1 and earlier.

## Features

- **Graphics:** SSAA (render scale), MSAA, FXAA/SMAA mode and quality, LOD bias, foliage LOD bias, depth priming mode
- **Click distances and radii:** switch stands, flares, couplers, gladhands, station agents, industry hover, coal chutes, water spouts and columns, diesel stands, and the track query tool
- **Time:** wait 1 hour, sleep until the interchange is served
- **Teleport:** to the dispatch station or any spawn point
- **Weather presets**
- **Game mode:** switching into Sandbox asks for confirmation, because 0.6 makes that conversion permanent
- **Sandbox (host only):** add or remove money, train info, repair car or train, fill or empty car or train, set any load

Click the small **U** button at the top left of the screen to open the panel. Settings (graphics and
click distances) open from the **Settings** button there and are saved to `settings.json` in the mod folder.

## Install

1. In Railroader, open **Preferences > Features > Experimental** and turn on **Allow Unsafe DLL Mods**, then restart the game.
2. Unzip the release into the game's `Mods` folder so you have `Railroader/Mods/Utilities/` containing
   `Info.json`, `Utilities.dll`, `0Harmony.dll` and `LICENSE`.
3. Enable it in the game's Mods screen.
4. If you still have Unity Mod Manager or the old 1.x `Utilities` folder installed, remove them.

Problems are logged to the game's `Player.log` with a `[Utilities]` prefix. Any patch or panel section
that no longer matches the game is switched off on its own and logged; the rest of the mod keeps working.

## Building

Requirements: the .NET SDK (6 or later) and Railroader 0.6+ installed (you compile against its DLLs).

1. Copy `Directory.Build.props.example` to `Directory.Build.props` (git-ignored) and set `GameDir` to the
   folder that contains `Railroader_Data`.
2. Build:

   ```
   dotnet build Utilities/Utilities.csproj -c Release
   ```

The build stages the mod folder at `build/Utilities/`, copies it into `<GameDir>/Mods/Utilities`
(set `CopyToGame` to `false` to skip this), and Release builds also write `dist/Utilities_v<version>.zip`.

## What changed in the 0.6 port

- Unity Mod Manager is gone: the entry point is now `ModEntryPoint` (`IModEntryPoint` + `IWorldLifecycle`),
  logging goes through the game's mod logger, and `Info.json` uses the new manifest format.
- Game code moved from `Assembly-CSharp.dll` to `Railroader.dll` (plus `Core.dll`, `KeyValue.Runtime.dll`).
  Optional assemblies are referenced only if they exist; the Nature Renderer foliage LOD compiles out if it's gone.
- Harmony now comes from the `Lib.Harmony` NuGet package and ships next to the mod.
- Settings moved from UMM's settings screen to an in-game IMGUI settings window and `settings.json`.
- Settings are applied when the world finishes loading (polled) instead of on the old `MapDidLoadEvent`.
- Each Harmony patch and UI section is isolated, so one API change doesn't take down the whole mod.
- Fixed: "Fill/Empty Train" used the selected car's slot count for every car in the train.
- Hose render distance stays removed (the culling API changed in 2025.1).
