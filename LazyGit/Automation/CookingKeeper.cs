using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Cooking: cooking stations and grills are loaded with raw food from nearby chests, kept in fuel where they
// burn any, and the food that is done is taken off before it burns.
internal static class CookingKeeper
{
    private const string KindSection = "Cooking Station Types";

    private static readonly HashSet<CookingStation> stations = new HashSet<CookingStation>();

    // The on/off setting of each kind of cooking station, by its prefab name
    private static readonly Dictionary<string, ConfigEntry<bool>> kinds = new Dictionary<string, ConfigEntry<bool>>();

    private static readonly AccessTools.FieldRef<CookingStation, ZNetView> viewOf = AccessTools.FieldRefAccess<CookingStation, ZNetView>("m_nview");
    private static readonly Func<CookingStation, bool> fireIsLit = AccessTools.MethodDelegate<Func<CookingStation, bool>>(AccessTools.Method(typeof(CookingStation), "IsFireLit"));

    public static void Track(CookingStation station)
    {
        stations.Add(station);
    }

    public static void TrackExisting()
    {
        foreach (CookingStation station in UnityEngine.Object.FindObjectsByType<CookingStation>(FindObjectsSortMode.None))
        {
            stations.Add(station);
        }
        if (ZNetScene.instance != null)
        {
            ListKinds(ZNetScene.instance);
        }
    }

    public static void ListKinds(ZNetScene scene)
    {
        foreach (GameObject prefab in scene.m_prefabs)
        {
            if (prefab == null)
            {
                continue;
            }
            CookingStation station = prefab.GetComponent<CookingStation>();
            Piece piece = prefab.GetComponent<Piece>();
            if (station != null && piece != null)
            {
                KindSetting(prefab.name, piece);
            }
        }
    }

    public static void DoRound()
    {
        stations.RemoveWhere(station => station == null);

        foreach (CookingStation station in stations.ToList())
        {
            try
            {
                Tend(station);
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not tend the cooking station at {station.transform.position}: {e.Message}");
            }
        }
    }

    private static void Tend(CookingStation station)
    {
        ZNetView view = viewOf(station);
        if (view == null || !view.IsValid() || station.m_slots == null)
        {
            return;
        }

        Piece piece = station.GetComponent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer() || !KindSetting(Utils.GetPrefabName(station.gameObject), piece).Value)
        {
            return;
        }

        ZDO zdo = view.GetZDO();
        float range = MainConfig.cookingChestRange.Value;

        // Is there anything to do here: food to take off, a free slot, room for fuel?
        bool work = false;
        for (int i = 0; i < station.m_slots.Length && !work; i++)
        {
            string item = zdo.GetString("slot" + i, "");
            work = item == "" || IsDone(station, item);
        }
        work |= station.m_useFuel && station.m_fuelItem != null && station.m_maxFuel > 0 && station.m_maxFuel - zdo.GetFloat(ZDOVars.s_fuel, 0f) >= 1f;
        if (!work)
        {
            return;
        }

        // Only the station's owner tends it, so two players never both load the same one. One that another player
        // owns is claimed now and tended from the next round on.
        if (!Ownership.Hold(view))
        {
            return;
        }

        if (MainConfig.cookingTakeDone.Value)
        {
            TakeDone(station, view, zdo);
        }

        if (station.m_useFuel && station.m_fuelItem != null && station.m_maxFuel > 0)
        {
            int room = Mathf.FloorToInt(station.m_maxFuel - zdo.GetFloat(ZDOVars.s_fuel, 0f));
            if (room > 0)
            {
                int got = ChestIndex.Take(station.transform.position, range, station.m_fuelItem.m_itemData.m_shared.m_name, room);
                for (int i = 0; i < got; i++)
                {
                    view.InvokeRPC("RPC_AddFuel");
                }
            }
        }

        if (MainConfig.cookingLoad.Value && (!station.m_requireFire || fireIsLit(station)))
        {
            Load(station, view, zdo, range);
        }
    }

    // Puts raw food in every free slot, as far as the chests have any
    private static void Load(CookingStation station, ZNetView view, ZDO zdo, float range)
    {
        int free = 0;
        for (int i = 0; i < station.m_slots.Length; i++)
        {
            if (zdo.GetString("slot" + i, "") == "")
            {
                free++;
            }
        }

        foreach (CookingStation.ItemConversion conversion in station.m_conversion)
        {
            if (free <= 0)
            {
                break;
            }
            if (conversion.m_from == null)
            {
                continue;
            }
            int got = ChestIndex.Take(station.transform.position, range, conversion.m_from.m_itemData.m_shared.m_name, free);
            for (int i = 0; i < got; i++)
            {
                view.InvokeRPC("RPC_AddItem", conversion.m_from.gameObject.name, false);
            }
            free -= got;
        }
    }

    // Takes off what is done (or burnt), one slot at a time: into a chest if one has room, otherwise dropped beside the
    // station the way a player's click does it
    private static void TakeDone(CookingStation station, ZNetView view, ZDO zdo)
    {
        Vector3 from = station.transform.position + station.transform.forward;
        for (int i = 0; i < station.m_slots.Length; i++)
        {
            string item = zdo.GetString("slot" + i, "");
            if (item == "" || !IsDone(station, item))
            {
                continue;
            }

            if (MainConfig.cookingToChests.Value)
            {
                // No room in any chest: the food stays on the station until there is (nothing is dropped)
                GameObject prefab = ObjectDB.instance.GetItemPrefab(item);
                if (ChestIndex.Put(station.transform.position, MainConfig.cookingChestRange.Value, prefab, 1) < 1)
                {
                    continue;
                }

                // The slot is emptied as the game does it when the food is taken
                zdo.Set("slot" + i, "");
                zdo.Set("slot" + i, 0f);
                zdo.Set("slotstatus" + i, 0);
                zdo.Set(ZDOVars.s_cheatedQueued + i, false);
                view.InvokeRPC(ZNetView.Everybody, "RPC_SetSlotVisual", i, "");
            }
            else
            {
                view.InvokeRPC("RPC_RemoveDoneItem", from, 1);
            }

            if (station.m_skill != Skills.SkillType.None)
            {
                Player.m_localPlayer.RaiseSkill(station.m_skill, 0.6f);
            }
        }
    }

    private static bool IsDone(CookingStation station, string item)
    {
        if (station.m_overCookedItem != null && item == station.m_overCookedItem.name)
        {
            return true;
        }
        CookingStation.ItemConversion conversion = station.m_conversion.FirstOrDefault(c => c.m_from != null && (c.m_from.gameObject.name == item || c.m_to != null && c.m_to.name == item));
        return conversion != null && conversion.m_to != null && conversion.m_to.name == item;
    }

    private static ConfigEntry<bool> KindSetting(string prefabName, Piece piece)
    {
        if (kinds.TryGetValue(prefabName, out ConfigEntry<bool> setting))
        {
            return setting;
        }

        Localization text = Localization.instance;
        string pieceName = text != null ? text.Localize(piece.m_name) : prefabName;
        setting = MainConfig.File.Bind(MainConfig.Section(KindSection), prefabName, true, $"{pieceName} is loaded with raw food from nearby chests, and the food that is done is taken off.");
        kinds[prefabName] = setting;
        return setting;
    }
}
