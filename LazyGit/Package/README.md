# 🦥 Lazy Git

Your base looks after itself. Fires and torches take their own fuel from your chests, so you never have to walk round with a stack of wood again.

More chores will follow in later updates: the aim is that everything tedious in a base can be left to a chest.

<br>

## ✅ Features:

### 🔥 Auto Fuel

  * Campfires, hearths, bonfires, torches, sconces, braziers, the hot tub and other fires that burn fuel take **their own fuel** (wood, resin, ...) from your chests.
  * Chests up to **30 m** away are used (a setting), the nearest first.
  * A fire is filled up **in one go** once its fuel is below half (a setting), so you hear it once, not all the time.
  * A **switch for each kind of fire**, listed in the settings once you are in a world.
  * Only the fires and chests **you built** are used: the world's own fires (villages, dungeons) are left alone, as are carts, ships, gravestones, other players' private chests and guild chests, and any chest that is open at the time.
  * Optionally uses the fuel you are carrying when the chests run out.
  * Works in multiplayer, and is client side: you do not need the server to have it, and it does not change gameplay for anyone else.
  * Translated into 9 languages.

### 🏭 Auto Stations

  * Smelters, blast furnaces, charcoal kilns, spinning wheels, windmills and eitr refineries are **kept stocked** from your chests: their fuel (charcoal, ...) and their raw material (ore, wood, flax, barley, ...) up to the most they can hold.
  * Chests up to **30 m** away are used (a setting), the nearest first.
  * A **switch for each kind of station**, listed in the settings once you are in a world.
  * Same rules as Auto Fuel: only what **you built**, only your chests, and it works in multiplayer.

### 🍖 Auto Cooking

  * Cooking stations, iron cooking stations and grills are **loaded with raw food** from your chests while their fire is lit.
  * Where they burn fuel, it is topped up from the chests too.
  * Food that is **done is taken off** before it can burn and put **into a chest** (one that already holds some first, then the nearest). If no chest has room, it is dropped beside the station.
  * Ovens are looked after by Auto Stations.
  * A **switch for each kind of cooking station**, and the same rules as the other chores: only what you built, only your chests.

### 🍺 Auto Fermenting

  * Fermentation barrels are **filled with mead base** from your chests as soon as they are empty.
  * When a barrel is **ready it is tapped**, and the mead goes **into a chest** (one that already holds some first, then the nearest). What no chest has room for comes out of the tap as usual.
  * A **switch for each kind of fermenter**, and the same rules as the other chores.

### 🏹 Auto Ballista

  * Ballistas you built are **restocked with bolts** from your chests, so the base defends itself without a trip round the walls.
  * A ballista that has some bolts keeps its kind of bolt; an empty one takes whichever kind the chests have.
  * Filled up in one go once it is below half (a setting), from chests up to **30 m** away.
  * A **switch for each kind of ballista**, and the same rules as the other chores.

### 🚪 Auto Close Doors

  * Doors and gates you built that are **left open** are closed again once **nobody has been near them** for 10 seconds (both settings).
  * Doors that need a key, and doors that cannot be closed, are left alone.
  * A **switch for each kind of door**. In multiplayer the door's owner closes it, so two players never fight over one.

### 🔨 Auto Repair

  * Worn and damaged pieces **you built**, within **40 m** of you, are repaired once their health is below **75%** (both settings), the way the hammer repairs them, so it costs no materials.
  * Not in a ward you have no access to, and a badly damaged base is mended over a few rounds rather than all at once.

<br>

## ⚙️ Settings:

Press **F7** in game to open the settings panel, or edit `BepInEx/config/MagicMike.LazyGit.cfg`.

| Setting | Default | What it does |
|---|---|---|
| Enable Plugin | On | Turns the mod on or off. Needs a restart. |
| Enable Auto Fuel | On | Fires take their fuel from your chests. |
| Chest Range | 30 | Fires take fuel from chests up to this many metres away. |
| Refill Below | 50 | A fire is filled up once its fuel is below this percentage. At 100 it is topped up whenever there is room for a piece of fuel. |
| Use Player Inventory | Off | Uses fuel you are carrying when the chests do not have enough. |
| Show Message | Off | Shows how much fuel the fires took, in the top-left corner. |
| Fire Types | On | One switch for each kind of fire. |
| Enable Auto Stations | On | Stations are kept stocked from your chests. |
| Chest Range (Auto Stations) | 30 | Stations take from chests up to this many metres away. |
| Station Types | On | One switch for each kind of station. |
| Enable Auto Cooking | On | Cooking stations are loaded and emptied automatically. |
| Chest Range (Auto Cooking) | 30 | Cooking stations take from chests up to this many metres away. |
| Load Raw Food | On | Raw food from the chests is put on the station while its fire is lit. |
| Take Off Cooked Food | On | Done food is taken off before it burns. |
| Put Cooked Food In Chests | On | Taken-off food goes into a chest in range; if none has room, or Off, it is dropped beside the station. |
| Cooking Station Types | On | One switch for each kind of cooking station. |
| Enable Auto Fermenting | On | Fermenters are filled and tapped automatically. |
| Chest Range (Auto Fermenting) | 30 | Fermenters use chests up to this many metres away. |
| Fill Empty Fermenters | On | An empty fermenter takes mead base from the chests. |
| Tap Ready Fermenters | On | A ready fermenter is tapped. |
| Put Drinks In Chests | On | Tapped drinks go into a chest in range; the rest comes out of the tap. |
| Fermenter Types | On | One switch for each kind of fermenter. |
| Enable Auto Ballista | On | Ballistas are restocked from your chests. |
| Chest Range (Auto Ballista) | 30 | Ballistas take from chests up to this many metres away. |
| Refill Below (Auto Ballista) | 50 | A ballista is filled up once its ammunition is below this percentage. |
| Ballista Types | On | One switch for each kind of ballista. |
| Enable Auto Close Doors | On | Open doors with nobody near them are closed. |
| Close After | 10 | Seconds an open door has had nobody near it before it is closed. |
| Player Distance | 6 | A door stays open while a player is within this many metres. |
| Door Types | On | One switch for each kind of door. |
| Enable Auto Repair | On | Damaged pieces near you are repaired. |
| Range | 40 | Pieces up to this many metres from you are repaired. |
| Repair Below | 75 | A piece is repaired once its health is below this percentage. |

<br>

## 🌍 Translations:

The translation files are in `BepInEx/config/LazyGit/Translations/<Language>/LazyGit.json`. Edit them, or add a language, and restart the game.

<br>

## 💶 Donations: 

If you enjoy using this Valheim mod, please consider making a small donation to help support its development and future updates.

Even £1 or $1 would be greatly appreciated and would help more than you might think!

Thank you for your support, and happy gaming! ⚔️  ...... (A Paypal account is NOT required!)

<a href="https://www.paypal.com/donate/?hosted_button_id=BN8HEWHRMAREU">![](https://imgup.uk/i/n7Sg7WK9.png)</a>
<br>
<br>
