using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using Jotunn.Extensions;

namespace LazyGit;

internal static class MainConfig
{
    public static ConfigFile File;

    public static ConfigEntry<bool> modEnabled;

    // Auto Fuel
    public static ConfigEntry<bool> fuelEnabled;
    public static ConfigEntry<float> fuelChestRange;
    public static ConfigEntry<float> fuelRefillBelow;
    public static ConfigEntry<bool> fuelFromInventory;
    public static ConfigEntry<bool> fuelMessage;

    // Auto Stations
    public static ConfigEntry<bool> stationEnabled;
    public static ConfigEntry<float> stationChestRange;

    // Auto Cooking
    public static ConfigEntry<bool> cookingEnabled;
    public static ConfigEntry<float> cookingChestRange;
    public static ConfigEntry<bool> cookingLoad;
    public static ConfigEntry<bool> cookingTakeDone;
    public static ConfigEntry<bool> cookingToChests;

    // Auto Fermenting
    public static ConfigEntry<bool> fermentEnabled;
    public static ConfigEntry<float> fermentChestRange;
    public static ConfigEntry<bool> fermentLoad;
    public static ConfigEntry<bool> fermentTap;
    public static ConfigEntry<bool> fermentToChests;

    // Auto Ballista
    public static ConfigEntry<bool> ballistaEnabled;
    public static ConfigEntry<float> ballistaChestRange;
    public static ConfigEntry<float> ballistaRefillBelow;

    // Auto Shield Generators
    public static ConfigEntry<bool> generatorEnabled;
    public static ConfigEntry<float> generatorChestRange;
    public static ConfigEntry<float> generatorRefillBelow;

    // Auto Close Doors
    public static ConfigEntry<bool> doorEnabled;
    public static ConfigEntry<float> doorCloseAfter;
    public static ConfigEntry<float> doorPlayerDistance;

    // Auto Collect
    public static ConfigEntry<bool> collectEnabled;
    public static ConfigEntry<float> collectChestRange;
    public static ConfigEntry<float> collectAt;
    public static ConfigEntry<bool> collectHoney;
    public static ConfigEntry<bool> collectSap;

    // Auto Repair Buildings
    public static ConfigEntry<bool> repairEnabled;
    public static ConfigEntry<float> repairRange;
    public static ConfigEntry<float> repairBelow;

    // Binds many settings with one save of the file at the end, not one save for each
    public static void Batch(System.Action binding)
    {
        bool saveOnSet = File.SaveOnConfigSet;
        File.SaveOnConfigSet = false;
        try
        {
            binding();
        }
        finally
        {
            File.SaveOnConfigSet = saveOnSet;
        }
        if (saveOnSet)
        {
            File.Save();
        }
    }

    // Auto Repair Gear
    public static ConfigEntry<bool> gearRepairEnabled;

    // Auto Shield
    public static ConfigEntry<bool> shieldEnabled;
    public static ConfigEntry<bool> shieldAutoEquip;
    public static ConfigEntry<bool> shieldAfterRanged;
    public static ConfigEntry<bool> shieldUnequipOnTwoHanded;
    public static ConfigEntry<bool> shieldUnequipOnSheath;
    public static ConfigEntry<bool> shieldUnequipOnDeselect;
    public static ConfigEntry<bool> shieldWithAxe;
    public static ConfigEntry<bool> shieldWithButcherKnife;
    public static ConfigEntry<string> shieldMode;
    public static ConfigEntry<string> shieldNamed;
    public static ConfigEntry<string> shieldWeapons;
    public static ConfigEntry<string> shieldNever;

    // Auto Replant
    public static ConfigEntry<bool> replantEnabled;
    public static ConfigEntry<float> replantChance;
    public static ConfigEntry<float> replantDelay;
    public static ConfigEntry<bool> replantRandom;
    public static ConfigEntry<bool> replantRemoveStumps;
    public static ConfigEntry<float> replantWorkbenchClearance;
    public static ConfigEntry<bool> replantSwamp;
    public static ConfigEntry<bool> replantIgnoreSpace;

    // The settings sections in the order they appear in the cfg file and in the settings panel. Each chore is followed
    // by the section with the on/off switch of each kind of piece it looks after (bound when a world is loaded).
    // BepInEx writes the cfg file with its sections sorted as text, so the numbers have two digits ("02 - ..."): with
    // one digit "10" would come before "2". Add every new section here.
    private static readonly string[] sectionOrder =
    {
        "General Settings",
        "Auto Fuel", "Fire Types",
        "Auto Stations", "Station Types",
        "Auto Cooking", "Cooking Station Types",
        "Auto Fermenting", "Fermenter Types",
        "Auto Ballista", "Ballista Types",
        "Auto Shield Generators", "Shield Generator Types",
        "Auto Close Doors", "Door Types",
        "Auto Collect",
        "Auto Repair Buildings",
        "Auto Repair Gear",
        "Auto Shield",
        "Auto Tree Replant",
    };

    public static string Section(string name)
    {
        int index = System.Array.IndexOf(sectionOrder, name);
        return index >= 0 ? $"{index + 1:00} - {name}" : $"99 - {name}";
    }

    public static void Init(ConfigFile config)
    {
        File = config;

        modEnabled = config.BindConfigInOrder(Section("General Settings"), "Enable Plugin", defaultValue: true, "Enable the LazyGit plugin.\n(Restart required. It is read before the game starts, so it is a local setting).\n", synced: false, sectionOrder: false, settingOrder: true);

        fuelEnabled = config.BindConfigInOrder(Section("Auto Fuel"), "Enable Auto Fuel", defaultValue: true, "Fires, torches, braziers and other fires that burn fuel take their own fuel (wood, resin, ...) from your chests, so you do not have to carry it round the base. Only the fires and torches you built are looked after.", synced: false, sectionOrder: false, settingOrder: true);
        fuelChestRange = config.BindConfigInOrder(Section("Auto Fuel"), "Chest Range", defaultValue: 30f, "A fire takes its fuel from chests up to this many metres away, the nearest first.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        fuelRefillBelow = config.BindConfigInOrder(Section("Auto Fuel"), "Refill Below", defaultValue: 50f, "A fire is filled up in one go once its fuel is below this percentage of what it holds. At 100 it is topped up as soon as there is room for a piece of fuel.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(5f, 100f));
        fuelFromInventory = config.BindConfigInOrder(Section("Auto Fuel"), "Use Player Inventory", defaultValue: false, "If On, fuel you are carrying is used when the chests do not have enough.", synced: false, sectionOrder: false, settingOrder: true);
        fuelMessage = config.BindConfigInOrder(Section("Auto Fuel"), "Show Message", defaultValue: false, "If On, a short message in the top-left corner says how much fuel the fires took.", synced: false, sectionOrder: false, settingOrder: true);

        stationEnabled = config.BindConfigInOrder(Section("Auto Stations"), "Enable Auto Stations", defaultValue: true, "Smelters, blast furnaces, charcoal kilns, spinning wheels, windmills and eitr refineries you built are kept stocked with their fuel and raw material from your chests.", synced: false, sectionOrder: false, settingOrder: true);
        stationChestRange = config.BindConfigInOrder(Section("Auto Stations"), "Chest Range", defaultValue: 30f, "A station takes what it needs from chests up to this many metres away, the nearest first.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));

        cookingEnabled = config.BindConfigInOrder(Section("Auto Cooking"), "Enable Auto Cooking", defaultValue: true, "Cooking stations and grills you built are loaded with raw food from your chests, kept in fuel where they burn any, and the food that is done is taken off before it burns. (Ovens are Auto Stations.)", synced: false, sectionOrder: false, settingOrder: true);
        cookingChestRange = config.BindConfigInOrder(Section("Auto Cooking"), "Chest Range", defaultValue: 30f, "A cooking station takes what it needs from chests up to this many metres away, the nearest first.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        cookingLoad = config.BindConfigInOrder(Section("Auto Cooking"), "Load Raw Food", defaultValue: true, "Raw food from the chests is put on the station while its fire is lit.", synced: false, sectionOrder: false, settingOrder: true);
        cookingTakeDone = config.BindConfigInOrder(Section("Auto Cooking"), "Take Off Cooked Food", defaultValue: true, "Food that is done is taken off before it burns.", synced: false, sectionOrder: false, settingOrder: true);
        cookingToChests = config.BindConfigInOrder(Section("Auto Cooking"), "Put Cooked Food In Chests", defaultValue: true, "Cooked food goes into a chest within range (one that already has some first, then the nearest). If no chest has room, the food stays on the station until there is: nothing is dropped. If this is Off, it is dropped beside the station.", synced: false, sectionOrder: false, settingOrder: true);

        fermentEnabled = config.BindConfigInOrder(Section("Auto Fermenting"), "Enable Auto Fermenting", defaultValue: true, "Fermenters you built are filled with mead base from your chests, and what is ready is tapped into a chest.", synced: false, sectionOrder: false, settingOrder: true);
        fermentChestRange = config.BindConfigInOrder(Section("Auto Fermenting"), "Chest Range", defaultValue: 30f, "A fermenter takes and puts things in chests up to this many metres away.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        fermentLoad = config.BindConfigInOrder(Section("Auto Fermenting"), "Fill Empty Fermenters", defaultValue: true, "An empty fermenter takes mead base from the chests.", synced: false, sectionOrder: false, settingOrder: true);
        fermentTap = config.BindConfigInOrder(Section("Auto Fermenting"), "Tap Ready Fermenters", defaultValue: true, "A fermenter that is ready is tapped, so it can be filled again.", synced: false, sectionOrder: false, settingOrder: true);
        fermentToChests = config.BindConfigInOrder(Section("Auto Fermenting"), "Put Drinks In Chests", defaultValue: true, "The tapped drinks go into a chest within range (one that already has some first, then the nearest). If the chests have no room for all of it, the fermenter waits until they do: nothing is dropped. If this is Off, it comes out of the tap.", synced: false, sectionOrder: false, settingOrder: true);

        ballistaEnabled = config.BindConfigInOrder(Section("Auto Ballista"), "Enable Auto Ballista", defaultValue: true, "Ballistas you built are restocked with bolts from your chests.", synced: false, sectionOrder: false, settingOrder: true);
        ballistaChestRange = config.BindConfigInOrder(Section("Auto Ballista"), "Chest Range", defaultValue: 30f, "A ballista takes ammunition from chests up to this many metres away, the nearest first.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        ballistaRefillBelow = config.BindConfigInOrder(Section("Auto Ballista"), "Refill Below", defaultValue: 50f, "A ballista is filled up in one go once its ammunition is below this percentage of what it holds. At 100 it is topped up as soon as there is room for a bolt.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(5f, 100f));

        generatorEnabled = config.BindConfigInOrder(Section("Auto Shield Generators"), "Enable Auto Shield Generators", defaultValue: true, "Shield generators you built are topped up with their fuel (black cores) from your chests.", synced: false, sectionOrder: false, settingOrder: true);
        generatorChestRange = config.BindConfigInOrder(Section("Auto Shield Generators"), "Chest Range", defaultValue: 30f, "A shield generator takes its fuel from chests up to this many metres away, the nearest first.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        generatorRefillBelow = config.BindConfigInOrder(Section("Auto Shield Generators"), "Refill Below", defaultValue: 50f, "A shield generator is filled up in one go once its fuel is below this percentage of what it holds. At 100 it is topped up as soon as there is room for a piece of fuel.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(5f, 100f));

        doorEnabled = config.BindConfigInOrder(Section("Auto Close Doors"), "Enable Auto Close Doors", defaultValue: true, "Doors and gates you built that are left open are closed again once nobody is near them.", synced: false, sectionOrder: false, settingOrder: true);
        doorCloseAfter = config.BindConfigInOrder(Section("Auto Close Doors"), "Close After", defaultValue: 10f, "How many seconds an open door has had nobody near it before it is closed. (Checked every 5 seconds, so it can take a little longer.)", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(5f, 300f));
        doorPlayerDistance = config.BindConfigInOrder(Section("Auto Close Doors"), "Player Distance", defaultValue: 6f, "A door is left open while any player is within this many metres of it.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(1f, 50f));

        collectEnabled = config.BindConfigInOrder(Section("Auto Collect"), "Enable Auto Collect", defaultValue: true, "Beehives and sap extractors you built are emptied into your chests, so you do not have to visit them.", synced: false, sectionOrder: false, settingOrder: true);
        collectChestRange = config.BindConfigInOrder(Section("Auto Collect"), "Chest Range", defaultValue: 30f, "The honey and sap go into chests up to this many metres away (one that already has some first, then the nearest). If no chest has room, they stay in the hive or extractor until there is.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        collectAt = config.BindConfigInOrder(Section("Auto Collect"), "Collect At", defaultValue: 100f, "A hive or extractor is emptied once it is this percentage full. At 100 it is emptied when full, so nothing is wasted.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(1f, 100f));
        collectHoney = config.BindConfigInOrder(Section("Auto Collect"), "Collect Honey", defaultValue: true, "Beehives are emptied.", synced: false, sectionOrder: false, settingOrder: true);
        collectSap = config.BindConfigInOrder(Section("Auto Collect"), "Collect Sap", defaultValue: true, "Sap extractors are emptied.", synced: false, sectionOrder: false, settingOrder: true);

        repairEnabled = config.BindConfigInOrder(Section("Auto Repair Buildings"), "Enable Auto Repair Buildings", defaultValue: true, "Worn and damaged pieces you built, near you, are repaired for you, the way the hammer repairs them (which needs no materials).", synced: false, sectionOrder: false, settingOrder: true);
        repairRange = config.BindConfigInOrder(Section("Auto Repair Buildings"), "Range", defaultValue: 40f, "Pieces up to this many metres from you are repaired.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        repairBelow = config.BindConfigInOrder(Section("Auto Repair Buildings"), "Repair Below", defaultValue: 75f, "A piece is repaired once its health is below this percentage. At 100 every scratch is repaired.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(5f, 100f));

        gearRepairEnabled = config.BindConfigInOrder(Section("Auto Repair Gear"), "Enable Auto Repair Gear", defaultValue: true, "Opening a crafting station repairs the equipment in your inventory that the station can repair, including what you are not wearing. The game's own rules and costs apply: a workbench does not repair forge gear.", synced: false, sectionOrder: false, settingOrder: true);

        shieldEnabled = config.BindConfigInOrder(Section("Auto Shield"), "Enable Auto Shield", defaultValue: true, "Drawing a one-handed weapon equips a shield with it.", synced: false, sectionOrder: false, settingOrder: true);
        shieldAutoEquip = config.BindConfigInOrder(Section("Auto Shield"), "Equip With One-Handed Weapons", defaultValue: true, "A shield is equipped whenever a one-handed weapon is.", synced: false, sectionOrder: false, settingOrder: true);
        shieldAfterRanged = config.BindConfigInOrder(Section("Auto Shield"), "Equip After Bow Or Crossbow", defaultValue: true, "Also equips a shield when you change from a bow, crossbow or other two-handed weapon to a one-handed one, even if the setting above is Off.", synced: false, sectionOrder: false, settingOrder: true);
        shieldUnequipOnTwoHanded = config.BindConfigInOrder(Section("Auto Shield"), "Put Away With Two-Handed Weapons", defaultValue: true, "The shield is put back in your inventory when a two-handed or ranged weapon is equipped.", synced: false, sectionOrder: false, settingOrder: true);
        shieldUnequipOnSheath = config.BindConfigInOrder(Section("Auto Shield"), "Put Away When Sheathing", defaultValue: false, "If On, sheathing your weapon (R) also puts the shield away, so only the weapon is on your back. If Off, both are shown on your back as in the game.", synced: false, sectionOrder: false, settingOrder: true);
        shieldUnequipOnDeselect = config.BindConfigInOrder(Section("Auto Shield"), "Put Away When Weapon Is Deselected", defaultValue: true, "The shield is put away when you put a one-handed weapon away with its hotbar key (not when you equip a shield on its own, and not when you throw a spear).", synced: false, sectionOrder: false, settingOrder: true);
        shieldWithAxe = config.BindConfigInOrder(Section("Auto Shield"), "Equip With Axes", defaultValue: false, "If Off, a one-handed axe gets no shield (so you can chop trees), and the shield is put away.", synced: false, sectionOrder: false, settingOrder: true);
        shieldWithButcherKnife = config.BindConfigInOrder(Section("Auto Shield"), "Equip With Butcher Knife", defaultValue: false, "If Off, the butcher knife gets no shield (for skinning), and the shield is put away.", synced: false, sectionOrder: false, settingOrder: true);
        shieldMode = config.BindConfigInOrder(Section("Auto Shield"), "Shield Choice", defaultValue: "First", "Which shield is equipped. First: the first one in your inventory. Best: the one that blocks best. Preferred: the one named below, or the best if you do not carry it.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueList<string>("First", "Best", "Preferred"));
        shieldNamed = config.BindConfigInOrder(Section("Auto Shield"), "Preferred Shield", defaultValue: "", "The shield used when the choice is Preferred, by its prefab name (for example ShieldWood).", synced: false, sectionOrder: false, settingOrder: true);
        shieldWeapons = config.BindConfigInOrder(Section("Auto Shield"), "Weapons", defaultValue: "", "Only these one-handed weapons get a shield: a list, separated by commas, of weapon types, skills or prefab names. Empty: all of them.", synced: false, sectionOrder: false, settingOrder: true);
        shieldNever = config.BindConfigInOrder(Section("Auto Shield"), "Never Use These Shields", defaultValue: "", "Shields that are never equipped automatically: a list of prefab names, separated by commas.", synced: false, sectionOrder: false, settingOrder: true);
        foreach (ConfigEntry<string> choice in new[] { shieldMode, shieldNamed, shieldNever })
        {
            choice.SettingChanged += (_, __) => ShieldKeeper.Reapply();
        }

        replantEnabled = config.BindConfigInOrder(Section("Auto Tree Replant"), "Enable Auto Replant", defaultValue: true, "A sapling is planted where a tree stump is destroyed, so the forest grows back. No seeds are needed.", synced: false, sectionOrder: false, settingOrder: true);
        replantChance = config.BindConfigInOrder(Section("Auto Tree Replant"), "Replant Chance", defaultValue: 100f, "The chance, in percent, that a tree is replanted.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(0f, 100f));
        replantDelay = config.BindConfigInOrder(Section("Auto Tree Replant"), "Delay", defaultValue: 2.5f, "Seconds to wait before the sapling is planted.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(0f, 60f));
        replantRandom = config.BindConfigInOrder(Section("Auto Tree Replant"), "Random Sapling", defaultValue: false, "If On, a sapling of a random kind is planted, not one of the kind that was cut.", synced: false, sectionOrder: false, settingOrder: true);
        replantRemoveStumps = config.BindConfigInOrder(Section("Auto Tree Replant"), "Remove Stumps", defaultValue: true, "Stumps disappear by themselves as soon as a tree is cut down (and the sapling is planted then).", synced: false, sectionOrder: false, settingOrder: true);
        replantWorkbenchClearance = config.BindConfigInOrder(Section("Auto Tree Replant"), "Keep Clear Of Workbenches", defaultValue: 0f, "No sapling is planted within this many metres of a workbench. 0 turns this off.", synced: false, sectionOrder: false, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(0f, 100f));
        replantSwamp = config.BindConfigInOrder(Section("Auto Tree Replant"), "Replant Swamp Trees", defaultValue: true, "Ancient (swamp) trees are replanted too.", synced: false, sectionOrder: false, settingOrder: true);
        replantIgnoreSpace = config.BindConfigInOrder(Section("Auto Tree Replant"), "Ignore Growth Space", defaultValue: false, "If On, saplings grow even when there is not enough room around them.", synced: false, sectionOrder: false, settingOrder: true);

        CarryOverOldSections(config);

        // Decimal settings rounded (and kept in range) whichever editor sets them
        MagicMike.ModSettings.ConfigRounding.RoundAll(config);
    }

    private static readonly Dictionary<string, string[]> OldSections = new Dictionary<string, string[]>
    {
    };

    private static void CarryOverOldSections(ConfigFile config)
    {
        Dictionary<ConfigDefinition, string> orphans;
        try
        {
            PropertyInfo property = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            orphans = property?.GetValue(config) as Dictionary<ConfigDefinition, string>;
        }
        catch (System.Exception e)
        {
            Jotunn.Logger.LogWarning("[LazyGit] Could not look for settings under old section names: " + e.Message);
            return;
        }
        if (orphans == null || orphans.Count == 0)
        {
            return;
        }

        int carried = 0, dropped = 0;
        HashSet<ConfigEntryBase> handled = new HashSet<ConfigEntryBase>();
        bool saveOnSet = config.SaveOnConfigSet;
        config.SaveOnConfigSet = false;
        try
        {
            // An earlier renumbering can leave the same setting under several old numbers; the highest is the latest
            foreach (ConfigDefinition old in orphans.Keys.OrderByDescending(d => SectionNumber(d.Section)).ToList())
            {
                string oldName = SectionName(old.Section);
                ConfigEntryBase entry = ((IDictionary<ConfigDefinition, ConfigEntryBase>)config).Values.FirstOrDefault(e => e.Definition.Key == old.Key && e.Definition.Section != old.Section
                    && (SectionName(e.Definition.Section) == oldName || OldSections.TryGetValue(SectionName(e.Definition.Section), out string[] before) && before.Contains(oldName)));
                if (entry == null)
                {
                    continue;   // (not one of ours any more: left as it is)
                }
                if (handled.Add(entry) && Equals(entry.BoxedValue, entry.DefaultValue))
                {
                    try
                    {
                        entry.SetSerializedValue(orphans[old]);
                        carried++;
                    }
                    catch (System.Exception e)
                    {
                        Jotunn.Logger.LogWarning("[LazyGit] Could not carry over " + old.Section + " / " + old.Key + ": " + e.Message);
                    }
                }
                orphans.Remove(old);
                dropped++;
            }
        }
        finally
        {
            config.SaveOnConfigSet = saveOnSet;
        }
        if (dropped > 0)
        {
            config.Save();
            Jotunn.Logger.LogInfo("[LazyGit] Carried " + carried + " setting(s) over from old section names in the config file.");
        }
    }

    // A section's name without the number in front ("4 - Fortified Ballista": Fortified Ballista)
    private static string SectionName(string section) => Regex.Replace(section ?? "", @"^\d+\s*-\s*", "");

    private static int SectionNumber(string section)
    {
        Match match = Regex.Match(section ?? "", @"^(\d+)\s*-");
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }
}
