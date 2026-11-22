using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Stations: the pieces that turn one item into another (smelters, kilns, spinning wheels, windmills, eitr
// refineries...) are kept stocked with their fuel and their raw material from nearby chests.
internal static class StationKeeper
{
    private const string KindSection = "Station Types";

    private static readonly HashSet<Smelter> stations = new HashSet<Smelter>();

    // The on/off setting of each kind of station, by its prefab name
    private static readonly Dictionary<string, ConfigEntry<bool>> kinds = new Dictionary<string, ConfigEntry<bool>>();

    private static readonly AccessTools.FieldRef<Smelter, ZNetView> viewOf = AccessTools.FieldRefAccess<Smelter, ZNetView>("m_nview");

    public static void Track(Smelter station)
    {
        stations.Add(station);
    }

    public static void TrackExisting()
    {
        foreach (Smelter station in Object.FindObjectsByType<Smelter>(FindObjectsSortMode.None))
        {
            stations.Add(station);
        }
        if (ZNetScene.instance != null)
        {
            ListStationKinds(ZNetScene.instance);
        }
    }

    // One setting for every kind of station, so each can be switched off on its own
    public static void ListStationKinds(ZNetScene scene)
    {
        foreach (GameObject prefab in MainConfig.VanillaFirst(scene.m_prefabs))
        {
            if (prefab == null)
            {
                continue;
            }
            Smelter station = prefab.GetComponent<Smelter>();
            Piece piece = prefab.GetComponent<Piece>();
            if (station != null && piece != null)
            {
                KindSetting(prefab.name, piece);
            }
        }
    }

    // Where the stations the player built are (for Auto Tidy)
    public static IEnumerable<Vector3> Positions()
    {
        return stations.Where(s => s != null && s.GetComponent<Piece>() != null && s.GetComponent<Piece>().IsPlacedByPlayer())
            .Select(s => s.transform.position).ToList();
    }

    public static void DoRound()
    {
        stations.RemoveWhere(station => station == null);

        foreach (Smelter station in stations.ToList())
        {
            try
            {
                Stock(station);
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not stock the station at {station.transform.position}: {e.Message}");
            }
        }
    }

    private static void Stock(Smelter station)
    {
        ZNetView view = viewOf(station);
        if (view == null || !view.IsValid())
        {
            return;
        }

        Piece piece = station.GetComponent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer() || !KindSetting(Utils.GetPrefabName(station.gameObject), piece).Value)
        {
            return;
        }

        float range = MainConfig.stationChestRange.Value;
        ZDO zdo = view.GetZDO();

        // What it has room for: fuel (charcoal for a smelter; none for a windmill or a spinning wheel), and raw
        // material (whatever the station can turn into something else, up to its queue)
        int fuelRoom = station.m_fuelItem != null && station.m_maxFuel > 0 ? Mathf.FloorToInt(station.m_maxFuel - zdo.GetFloat(ZDOVars.s_fuel, 0f)) : 0;
        int oreRoom = station.m_maxOre > 0 ? station.m_maxOre - zdo.GetInt(ZDOVars.s_queued, 0) : 0;
        bool fuelWork = fuelRoom > 0 && ChestIndex.Has(station.transform.position, range, station.m_fuelItem.m_itemData.m_shared.m_name);
        bool oreWork = oreRoom > 0 && station.m_conversion.Any(c => c.m_from != null && ChestIndex.Has(station.transform.position, range, c.m_from.m_itemData.m_shared.m_name));
        if (MainConfig.debugLog.Value)
        {
            Jotunn.Logger.LogInfo($"[{LazyGitMod.PluginName}] Station {Utils.GetPrefabName(station.gameObject)} at {station.transform.position}: fuel {zdo.GetFloat(ZDOVars.s_fuel, 0f)}/{station.m_maxFuel}, queue {zdo.GetInt(ZDOVars.s_queued, 0)}/{station.m_maxOre}, fuel work {fuelWork}, ore work {oreWork}, owner {view.IsOwner()}, range {range}");
        }
        if (!fuelWork && !oreWork)
        {
            return;   // (nothing to put in that the chests have: the station is not claimed)
        }

        // Only the station's owner stocks it, so two players never both fill the same one. One that another player
        // owns is claimed now and stocked from the next round on.
        if (!Ownership.Hold(view))
        {
            return;
        }

        if (fuelRoom > 0)
        {
            int got = ChestIndex.Take(station.transform.position, range, station.m_fuelItem.m_itemData.m_shared.m_name, fuelRoom);
            for (int i = 0; i < got; i++)
            {
                view.InvokeRPC("RPC_AddFuel");
            }
        }

        if (oreRoom > 0)
        {
            int room = oreRoom;
            foreach (Smelter.ItemConversion conversion in station.m_conversion)
            {
                if (room <= 0)
                {
                    break;
                }
                if (conversion.m_from == null)
                {
                    continue;
                }
                int got = ChestIndex.Take(station.transform.position, range, conversion.m_from.m_itemData.m_shared.m_name, room);
                for (int i = 0; i < got; i++)
                {
                    view.InvokeRPC("RPC_AddOre", conversion.m_from.gameObject.name, false);
                }
                room -= got;
            }
        }
    }

    private static ConfigEntry<bool> KindSetting(string prefabName, Piece piece)
    {
        if (kinds.TryGetValue(prefabName, out ConfigEntry<bool> setting))
        {
            return setting;
        }

        string pieceName = EnglishText.Of(piece.m_name, prefabName);
        setting = MainConfig.BindKind(KindSection, prefabName, pieceName, $"{pieceName} is kept stocked with fuel and raw material from nearby chests.");
        kinds[prefabName] = setting;
        return setting;
    }
}
