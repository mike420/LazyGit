# LazyGit (Valheim mod)

Automates base chores so a chest does them: fires take fuel from chests first, then stations, ovens, cooking stations,
fermenters, ballistas, doors and repair. Required on the server and on every client (Jotunn `EveryoneMustHaveMod`, `VersionStrictness.Minor`: the server refuses players without it or with another minor version, and players cannot join a server without it); the chores themselves run in the players' games (the server has no local player). Author: MagicMike. Same layout and conventions as the
sibling mod `E:\Valheim_Mods\ValheimSkies` (use it as the reference for anything structural).

## Hard rules

- **A chore that stores items in a chest never drops them on the ground.** With no room it waits (`ChestIndex.RoomFor` before changing anything, or `Put` returning 0), and the item stays where it is.
- **All code must be original. Do not copy code from the reference mods.** They are in `G:\Valheim_Stuff\Auto` (and `G:\Valheim_Stuff\Auto\New folder`)
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
- `LazyGit/Automation/Ownership.cs`: `Hold(view)`: true if the local player owns the object, otherwise claims it (at most once per 30 s) and the chore goes ahead next round. **A chore claims a piece only once it knows there is work to do there**, never just to look at it. Repair needs none (its RPC is sent to the owner and is harmless twice).
- `LazyGit/Automation/ChestIndex.cs`: the rules for which chests a chore may take from. **Every chore must take items through it** (`Take`; `Put` for putting items in).
- `LazyGit/Automation/FuelKeeper.cs`: Auto Fuel.
- `LazyGit/Automation/StationKeeper.cs`: Auto Stations (every `Smelter`: fuel via `RPC_AddFuel`, ore via `RPC_AddOre(prefabName, false)`, one call per item; per-kind switches in "Station Types"). Ovens are Smelters, so they are covered here. New .cs files must be added to `LazyGit.csproj` by hand.
- `LazyGit/Automation/CookingKeeper.cs`: Auto Cooking (every `CookingStation`: fuel `RPC_AddFuel`, raw food `RPC_AddItem(prefabName, false)` per free slot while `IsFireLit` (private, via delegate), done food into a chest via `ChestIndex.Put` (slot cleared through the ZDO) and if none has room it waits (only with "Put Cooked Food In Chests" off is it popped with `RPC_RemoveDoneItem(point, 1)`); switches in "Cooking Station Types").
- `LazyGit/Automation/FermentKeeper.cs`: Auto Fermenting (every `Fermenter`: empty = `s_content` 0 -> take one `m_from` and `RPC_AddItem(nameHash, false)`; ready = `s_startTime` older than `m_fermentationDuration` -> if `ChestIndex.RoomFor` the `m_producedItems`, clear the ZDO like `RPC_Tap` and `Put` them (otherwise it waits; only with the setting off are they instantiated at `m_outputPoint`); switches in "Fermenter Types").
- `LazyGit/Automation/BallistaKeeper.cs`: Auto Ballista (every `Turret` with `m_maxAmmo` > 0: ammo in `ZDOVars.s_ammo`, one `RPC_AddAmmo(prefabName)` per bolt; a loaded turret keeps its `GetAmmoType()`, an empty one takes the first `m_allowedAmmo` the chests have; switches in "Ballista Types").
- `LazyGit/Automation/DoorKeeper.cs`: Auto Close Doors (every `Door` with no key and not `m_canNotBeClosed`: open = `s_state` != 0; closed with `InvokeRPC("UseDoor", true)`; only the door's owner acts, or two clients would toggle it open again (see Ownership); the idle timer uses game time; switches in "Door Types").
- `LazyGit/Automation/RepairKeeper.cs`: Auto Repair Buildings (every `WearNTear` of a player-built `Piece` within range of the player and under "Repair Below": `WearNTear.Repair()`, free like the hammer; at most 30 per round; no per-kind switches, there are too many pieces).
- `LazyGit/Automation/CollectKeeper.cs`: Auto Collect (`Beehive` and `SapCollector`, which is not a Smelter: product built up in `ZDOVars.s_level`; when at least "Collect At" full, `ChestIndex.Put` the product (`level * Game.ScaleDrops`) and lower `s_level` by the units that went in; the game's own extract would drop it on the ground; only the owner acts).
- `LazyGit/Automation/GearRepair.cs`: Auto Repair Gear (postfix on `CraftingStation.Interact`; while `InventoryGui.HaveRepairableItems()`, `RepairOneItem()`, at most once per item carried; the game's own rules and costs).
- `LazyGit/Automation/ShieldKeeper.cs`: Auto Shield (not a chest chore: watches the hands through postfixes on `Humanoid.EquipItem/UnequipItem/HideHandItems/ShowHandItems` and `Attack.OnAttackTrigger`, plus a look every 0.75 s from `LazyGitMod.Update`; `Busy` marks its own equipment changes; there is no Alt+S popup, the F7 panel is the menu).
- `LazyGit/Automation/TreeKeeper.cs`: Auto Replant (not a chest chore: prefix on `Destructible.Destroy` plants the sapling in a coroutine on the plugin, only by the stump's owner, from a built-in table of stump -> sapling names; prefix on `TreeBase.SpawnLog` sets the stump prefab's `m_ttl` so stumps vanish; postfix on `Plant.HaveGrowSpace`).
- `LazyGit/Translations.cs` + `Assets/Translations/<Language>/LazyGit.json`: 9 languages (English, Chinese, French, German, Polish, Portuguese_Brazilian, Russian, Spanish, Turkish). Every new message key goes in all 9.
- `Shared/`: git submodule (`github.com/mike420/ValheimShared`), the F7 settings panel. Needs `git submodule update --init`.
- `LazyGit/Package/`: Thunderstore README, CHANGELOG, manifest, icon (256x256 PNG, the real one). `Nexus/README.txt` is generated from the Package README by `UpdateNexusReadme.ps1` on Release builds. Add each new feature to the Package README (features + settings table) and the changelog, and bump the version.

## Building

`"D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" LazyGit.sln -restore -p:Configuration=Debug -v:minimal -nologo`
Debug deploys the DLL and translations into the game's `BepInEx`; Release also makes the zip in `LazyGit/ThunderStore/`.
Plain `dotnet build` cannot resolve the game references. The game's log is `D:\SteamLibrary\steamapps\common\Valheim\BepInEx\LogOutput.log`.

## Design notes (from the reference mods and the game's code)

- Fires: `Fireplace` (`m_fuelItem`, `m_maxFuel`, `m_infiniteFuel`, `m_canRefill`), fuel in the ZDO (`ZDOVars.s_fuel`); add fuel with `nview.InvokeRPC("RPC_AddFuelAmount", float)`. Only the fire's owner feeds it (no double fills in multiplayer); only `Piece.IsPlacedByPlayer()` fires.
- Chests: `Container` (`m_privacy` Public/Private/Group, `m_wagon`, `m_checkGuardStone`, `IsInUse()`); take with `Inventory.RemoveItem(name, n, -1, false)` (the container saves itself when changed, if we own it); a chest owned by someone else is `ClaimOwnership()`ed and used next round.
- Many per-kind settings are bound at once: wrap them in `MainConfig.Batch` (one save of the cfg file, not one each).
- Tracking: Harmony postfixes on `Awake` keep our own sets of fires and chests, so a round never searches the scene.

## Status

Done: project set up, builds; Auto Fuel and Auto Stations (smelters, ovens, kilns, spinning wheels, windmills, eitr refineries), Auto Cooking, Auto Fermenting, Auto Ballista, Auto Close Doors, Auto Repair Buildings, Auto Collect, Auto Repair Gear, Auto Shield, Auto Replant written. **Not yet tested in the game.** Pushed to `github.com/mike420/LazyGit` (`origin`, `main`). **Not released yet: keep the version at 1.0.0 and add new features to the 1.0.0 changelog entry until the first release.**

Next: test every chore in the game. All the chore groups in the first plan are written, and the icon is done.
