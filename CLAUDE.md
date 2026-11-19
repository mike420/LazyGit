# LazyGit (Valheim mod)

Automates base chores so a chest does them: fires take fuel from chests first, then stations, ovens, cooking stations,
fermenters, ballistas, doors and repair. Client side only. Author: MagicMike. Same layout and conventions as the
sibling mod `E:\Valheim_Mods\ValheimSkies` (use it as the reference for anything structural).

## Hard rules

- **All code must be original. Do not copy code from the reference mods.** They are in `G:\Valheim_Stuff\Auto`
  (DigiTeam Auto Fuel/Oven/Fermenter/Processing/Cooking Station/Close Doors/Repair, Majestic StationAutoPopper,
  CRNR BallistaRestock, SimplifyDave AutoRefillFires, blacks7ar LazyVikings). They are known to work: read their
  READMEs and decompile them to see *how a chore behaves and which game APIs it uses*, then write it yourself, with
  our own structure and wording. Decompile into the scratchpad folder, never into this repo
  (`ilspycmd.cmd <dll> -o <dir>`). Game code: `D:\SteamLibrary\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll`
  (`ilspycmd.cmd assembly_valheim.dll -t Fireplace -o <dir>` for one type).
- Ask before committing or pushing, unless told to ("commit and push" is an explicit go-ahead).

## Layout

- `LazyGit/LazyGitMod.cs`: plugin (GUID `MagicMike.LazyGit`, version in `PluginVersion`). `Update` runs a chore round every 5 s.
- `LazyGit/MainConfig.cs`: settings via Jotunn `BindConfigInOrder` (numbered sections), plus `CarryOverOldSections` so renumbering sections keeps users' values. Per-fire-type switches are bound dynamically in section "Fire Types".
- `LazyGit/Patches.cs`: Harmony postfixes that register fires, chests and the fire kinds.
- `LazyGit/Automation/ChestIndex.cs`: the rules for which chests a chore may take from. **Every chore must take items through it** (`Take`; `Put` for putting items in).
- `LazyGit/Automation/FuelKeeper.cs`: Auto Fuel.
- `LazyGit/Automation/StationKeeper.cs`: Auto Stations (every `Smelter`: fuel via `RPC_AddFuel`, ore via `RPC_AddOre(prefabName, false)`, one call per item; per-kind switches in "Station Types"). Ovens are Smelters, so they are covered here. New .cs files must be added to `LazyGit.csproj` by hand.
- `LazyGit/Automation/CookingKeeper.cs`: Auto Cooking (every `CookingStation`: fuel `RPC_AddFuel`, raw food `RPC_AddItem(prefabName, false)` per free slot while `IsFireLit` (private, via delegate), done food into a chest via `ChestIndex.Put` (slot cleared through the ZDO) or, if none has room, `RPC_RemoveDoneItem(point, 1)`; switches in "Cooking Station Types").
- `LazyGit/Automation/FermentKeeper.cs`: Auto Fermenting (every `Fermenter`: empty = `s_content` 0 -> take one `m_from` and `RPC_AddItem(nameHash, false)`; ready = `s_startTime` older than `m_fermentationDuration` -> clear the ZDO like `RPC_Tap` and `ChestIndex.Put` the `m_producedItems`, the rest instantiated at `m_outputPoint`; switches in "Fermenter Types").
- `LazyGit/Translations.cs` + `Assets/Translations/<Language>/LazyGit.json`: 9 languages (English, Chinese, French, German, Polish, Portuguese_Brazilian, Russian, Spanish, Turkish). Every new message key goes in all 9.
- `Shared/`: git submodule (`github.com/mike420/ValheimShared`), the F7 settings panel. Needs `git submodule update --init`.
- `LazyGit/Package/`: Thunderstore README, CHANGELOG, manifest, icon (**icon.png is still a placeholder copied from ValheimSkies**). `Nexus/README.txt` is generated from the Package README by `UpdateNexusReadme.ps1` on Release builds. Add each new feature to the Package README (features + settings table) and the changelog, and bump the version.

## Building

`"D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" LazyGit.sln -restore -p:Configuration=Debug -v:minimal -nologo`
Debug deploys the DLL and translations into the game's `BepInEx`; Release also makes the zip in `LazyGit/ThunderStore/`.
Plain `dotnet build` cannot resolve the game references. The game's log is `D:\SteamLibrary\steamapps\common\Valheim\BepInEx\LogOutput.log`.

## Design notes (from the reference mods and the game's code)

- Fires: `Fireplace` (`m_fuelItem`, `m_maxFuel`, `m_infiniteFuel`, `m_canRefill`), fuel in the ZDO (`ZDOVars.s_fuel`); add fuel with `nview.InvokeRPC("RPC_AddFuelAmount", float)`. Only the fire's owner feeds it (no double fills in multiplayer); only `Piece.IsPlacedByPlayer()` fires.
- Chests: `Container` (`m_privacy` Public/Private/Group, `m_wagon`, `m_checkGuardStone`, `IsInUse()`); take with `Inventory.RemoveItem(name, n, -1, false)` (the container saves itself when changed, if we own it); a chest owned by someone else is `ClaimOwnership()`ed and used next round.
- Tracking: Harmony postfixes on `Awake` keep our own sets of fires and chests, so a round never searches the scene.

## Status

Done: project set up, builds; Auto Fuel and Auto Stations (smelters, ovens, kilns, spinning wheels, windmills, eitr refineries), Auto Cooking, Auto Fermenting written. **Not yet tested in the game.** Pushed to `github.com/mike420/LazyGit` (`origin`, `main`). **Not released yet: keep the version at 1.0.0 and add new features to the 1.0.0 changelog entry until the first release.**

Next: test both in the game; then the other chore groups (sap extractors and beehives; ballista
restock; auto-close doors; auto repair). Replace the icon.
