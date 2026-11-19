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

    // Auto Close Doors
    public static ConfigEntry<bool> doorEnabled;
    public static ConfigEntry<float> doorCloseAfter;
    public static ConfigEntry<float> doorPlayerDistance;

    // Auto Repair
    public static ConfigEntry<bool> repairEnabled;
    public static ConfigEntry<float> repairRange;
    public static ConfigEntry<float> repairBelow;

    // Auto Collect
    public static ConfigEntry<bool> collectEnabled;
    public static ConfigEntry<float> collectChestRange;
    public static ConfigEntry<float> collectAt;
    public static ConfigEntry<bool> collectHoney;
    public static ConfigEntry<bool> collectSap;

    public static void Init(ConfigFile config)
    {
        File = config;

        modEnabled = config.BindConfigInOrder("General Settings", "Enable Plugin", defaultValue: true, "Enable the LazyGit plugin.\n(Restart required. It is read before the game starts, so it is a local setting).\n", synced: false, sectionOrder: true, settingOrder: true);

        fuelEnabled = config.BindConfigInOrder("Auto Fuel", "Enable Auto Fuel", defaultValue: true, "Fires, torches, braziers and other fires that burn fuel take their own fuel (wood, resin, ...) from your chests, so you do not have to carry it round the base. Only the fires and torches you built are looked after.", synced: false, sectionOrder: true, settingOrder: true);
        fuelChestRange = config.BindConfigInOrder("Auto Fuel", "Chest Range", defaultValue: 30f, "A fire takes its fuel from chests up to this many metres away, the nearest first.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        fuelRefillBelow = config.BindConfigInOrder("Auto Fuel", "Refill Below", defaultValue: 50f, "A fire is filled up in one go once its fuel is below this percentage of what it holds. At 100 it is topped up as soon as there is room for a piece of fuel.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(5f, 100f));
        fuelFromInventory = config.BindConfigInOrder("Auto Fuel", "Use Player Inventory", defaultValue: false, "If On, fuel you are carrying is used when the chests do not have enough.", synced: false, sectionOrder: true, settingOrder: true);
        fuelMessage = config.BindConfigInOrder("Auto Fuel", "Show Message", defaultValue: false, "If On, a short message in the top-left corner says how much fuel the fires took.", synced: false, sectionOrder: true, settingOrder: true);

        stationEnabled = config.BindConfigInOrder("Auto Stations", "Enable Auto Stations", defaultValue: true, "Smelters, blast furnaces, charcoal kilns, spinning wheels, windmills and eitr refineries you built are kept stocked with their fuel and raw material from your chests.", synced: false, sectionOrder: true, settingOrder: true);
        stationChestRange = config.BindConfigInOrder("Auto Stations", "Chest Range", defaultValue: 30f, "A station takes what it needs from chests up to this many metres away, the nearest first.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));

        cookingEnabled = config.BindConfigInOrder("Auto Cooking", "Enable Auto Cooking", defaultValue: true, "Cooking stations and grills you built are loaded with raw food from your chests, kept in fuel where they burn any, and the food that is done is taken off before it burns. (Ovens are Auto Stations.)", synced: false, sectionOrder: true, settingOrder: true);
        cookingChestRange = config.BindConfigInOrder("Auto Cooking", "Chest Range", defaultValue: 30f, "A cooking station takes what it needs from chests up to this many metres away, the nearest first.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        cookingLoad = config.BindConfigInOrder("Auto Cooking", "Load Raw Food", defaultValue: true, "Raw food from the chests is put on the station while its fire is lit.", synced: false, sectionOrder: true, settingOrder: true);
        cookingTakeDone = config.BindConfigInOrder("Auto Cooking", "Take Off Cooked Food", defaultValue: true, "Food that is done is taken off before it burns.", synced: false, sectionOrder: true, settingOrder: true);
        cookingToChests = config.BindConfigInOrder("Auto Cooking", "Put Cooked Food In Chests", defaultValue: true, "Cooked food goes into a chest within range (one that already has some first, then the nearest). If no chest has room, or this is Off, it is dropped beside the station.", synced: false, sectionOrder: true, settingOrder: true);

        fermentEnabled = config.BindConfigInOrder("Auto Fermenting", "Enable Auto Fermenting", defaultValue: true, "Fermenters you built are filled with mead base from your chests, and what is ready is tapped into a chest.", synced: false, sectionOrder: true, settingOrder: true);
        fermentChestRange = config.BindConfigInOrder("Auto Fermenting", "Chest Range", defaultValue: 30f, "A fermenter takes and puts things in chests up to this many metres away.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        fermentLoad = config.BindConfigInOrder("Auto Fermenting", "Fill Empty Fermenters", defaultValue: true, "An empty fermenter takes mead base from the chests.", synced: false, sectionOrder: true, settingOrder: true);
        fermentTap = config.BindConfigInOrder("Auto Fermenting", "Tap Ready Fermenters", defaultValue: true, "A fermenter that is ready is tapped, so it can be filled again.", synced: false, sectionOrder: true, settingOrder: true);
        fermentToChests = config.BindConfigInOrder("Auto Fermenting", "Put Drinks In Chests", defaultValue: true, "The tapped drinks go into a chest within range (one that already has some first, then the nearest). What no chest has room for comes out of the tap.", synced: false, sectionOrder: true, settingOrder: true);

        ballistaEnabled = config.BindConfigInOrder("Auto Ballista", "Enable Auto Ballista", defaultValue: true, "Ballistas you built are restocked with bolts from your chests.", synced: false, sectionOrder: true, settingOrder: true);
        ballistaChestRange = config.BindConfigInOrder("Auto Ballista", "Chest Range", defaultValue: 30f, "A ballista takes ammunition from chests up to this many metres away, the nearest first.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        ballistaRefillBelow = config.BindConfigInOrder("Auto Ballista", "Refill Below", defaultValue: 50f, "A ballista is filled up in one go once its ammunition is below this percentage of what it holds. At 100 it is topped up as soon as there is room for a bolt.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(5f, 100f));

        doorEnabled = config.BindConfigInOrder("Auto Close Doors", "Enable Auto Close Doors", defaultValue: true, "Doors and gates you built that are left open are closed again once nobody is near them.", synced: false, sectionOrder: true, settingOrder: true);
        doorCloseAfter = config.BindConfigInOrder("Auto Close Doors", "Close After", defaultValue: 10f, "How many seconds an open door has had nobody near it before it is closed. (Checked every 5 seconds, so it can take a little longer.)", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(5f, 300f));
        doorPlayerDistance = config.BindConfigInOrder("Auto Close Doors", "Player Distance", defaultValue: 6f, "A door is left open while any player is within this many metres of it.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(1f, 50f));

        repairEnabled = config.BindConfigInOrder("Auto Repair", "Enable Auto Repair", defaultValue: true, "Worn and damaged pieces you built, near you, are repaired for you, the way the hammer repairs them (which needs no materials).", synced: false, sectionOrder: true, settingOrder: true);
        repairRange = config.BindConfigInOrder("Auto Repair", "Range", defaultValue: 40f, "Pieces up to this many metres from you are repaired.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        repairBelow = config.BindConfigInOrder("Auto Repair", "Repair Below", defaultValue: 75f, "A piece is repaired once its health is below this percentage. At 100 every scratch is repaired.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(5f, 100f));

        collectEnabled = config.BindConfigInOrder("Auto Collect", "Enable Auto Collect", defaultValue: true, "Beehives and sap extractors you built are emptied into your chests, so you do not have to visit them.", synced: false, sectionOrder: true, settingOrder: true);
        collectChestRange = config.BindConfigInOrder("Auto Collect", "Chest Range", defaultValue: 30f, "The honey and sap go into chests up to this many metres away (one that already has some first, then the nearest). If no chest has room, they stay in the hive or extractor.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(2f, 200f));
        collectAt = config.BindConfigInOrder("Auto Collect", "Collect At", defaultValue: 100f, "A hive or extractor is emptied once it is this percentage full. At 100 it is emptied when full, so nothing is wasted.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(1f, 100f));
        collectHoney = config.BindConfigInOrder("Auto Collect", "Collect Honey", defaultValue: true, "Beehives are emptied.", synced: false, sectionOrder: true, settingOrder: true);
        collectSap = config.BindConfigInOrder("Auto Collect", "Collect Sap", defaultValue: true, "Sap extractors are emptied.", synced: false, sectionOrder: true, settingOrder: true);

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
