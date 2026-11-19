# LazyGit

Valheim mod that automates the chores of a base, so a chest can do them for you. Client side only.

Written from scratch. The existing automation mods (Auto Fuel, Auto Refill Fires, LazyVikings and others) were only
used to see how each chore behaves in the game; no code was taken from them.

## Chores so far

- **Auto Fuel** (`Automation/FuelKeeper.cs`): fires and torches the player built take their fuel from nearby chests.

`Automation/ChestIndex.cs` holds the rules for which chests a chore may use (built by a player, not a cart, ship or
gravestone, not another player's private or a guild chest, not open right now, and owned by the local player in the
network). Every chore takes things from chests through it.

## Planned

Stations (smelters, kilns, spinning wheels, windmills, eitr refineries, sap extractors, beehives), ovens, cooking
stations, fermenters, ballistas, doors and repair.

## Building

VS MSBuild `LazyGit.sln -restore -p:Configuration=Release`. The Release build deploys to the game, updates the
manifest and the Nexus README, and makes the Thunderstore zip in `LazyGit/ThunderStore/`.

`Shared` is a git submodule (the settings panel): `git submodule update --init`.
