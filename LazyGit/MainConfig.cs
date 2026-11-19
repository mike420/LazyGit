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
