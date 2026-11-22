using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Fermenting: fermenters are filled with what they ferment (mead base) from nearby chests, and what is ready
// is tapped into a chest.
internal static class FermentKeeper
{
    private const string KindSection = "Fermenter Types";

    private static readonly HashSet<Fermenter> fermenters = new HashSet<Fermenter>();

    // The on/off setting of each kind of fermenter, by its prefab name
    private static readonly Dictionary<string, ConfigEntry<bool>> kinds = new Dictionary<string, ConfigEntry<bool>>();

    private static readonly AccessTools.FieldRef<Fermenter, ZNetView> viewOf = AccessTools.FieldRefAccess<Fermenter, ZNetView>("m_nview");

    public static void Track(Fermenter fermenter)
    {
        fermenters.Add(fermenter);
    }

    public static void TrackExisting()
    {
        foreach (Fermenter fermenter in UnityEngine.Object.FindObjectsByType<Fermenter>(FindObjectsSortMode.None))
        {
            fermenters.Add(fermenter);
        }
        if (ZNetScene.instance != null)
        {
            ListKinds(ZNetScene.instance);
        }
    }

    public static void ListKinds(ZNetScene scene)
    {
        foreach (GameObject prefab in MainConfig.VanillaFirst(scene.m_prefabs))
        {
            if (prefab == null)
            {
                continue;
            }
            Fermenter fermenter = prefab.GetComponent<Fermenter>();
            Piece piece = prefab.GetComponent<Piece>();
            if (fermenter != null && piece != null)
            {
                KindSetting(prefab.name, piece);
            }
        }
    }

    // Where the stations the player built are (for Auto Tidy)
    public static IEnumerable<Vector3> Positions()
    {
        return fermenters.Where(s => s != null && s.GetComponent<Piece>() != null && s.GetComponent<Piece>().IsPlacedByPlayer())
            .Select(s => s.transform.position).ToList();
    }

    public static void DoRound()
    {
        fermenters.RemoveWhere(fermenter => fermenter == null);

        foreach (Fermenter fermenter in fermenters.ToList())
        {
            try
            {
                Tend(fermenter);
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not tend the fermenter at {fermenter.transform.position}: {e.Message}");
            }
        }
    }

    private static void Tend(Fermenter fermenter)
    {
        ZNetView view = viewOf(fermenter);
        if (view == null || !view.IsValid())
        {
            return;
        }

        Piece piece = fermenter.GetComponent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer() || !KindSetting(Utils.GetPrefabName(fermenter.gameObject), piece).Value)
        {
            return;
        }

        ZDO zdo = view.GetZDO();
        float range = MainConfig.fermentChestRange.Value;
        int content = zdo.GetInt(ZDOVars.s_content, 0);

        // Nothing to do while it is fermenting; nothing that can be done (no mead base in the chests, no room for the
        // drink) means it is not claimed
        if (!HasWork(fermenter, zdo, content, range))
        {
            return;
        }

        // Only the fermenter's owner tends it, so two players never both work the same one. One that another player
        // owns is claimed now and tended from the next round on.
        if (!Ownership.Hold(view))
        {
            return;
        }

        if (content == 0)
        {
            if (MainConfig.fermentLoad.Value)
            {
                Fill(fermenter, view, range);
            }
        }
        else if (MainConfig.fermentTap.Value && IsReady(fermenter, zdo))
        {
            Tap(fermenter, zdo, content, range);
        }
    }

    private static bool HasWork(Fermenter fermenter, ZDO zdo, int content, float range)
    {
        Vector3 at = fermenter.transform.position;
        if (content == 0)
        {
            return MainConfig.fermentLoad.Value
                && fermenter.m_conversion.Any(c => c.m_from != null && ChestIndex.Has(at, range, c.m_from.m_itemData.m_shared.m_name));
        }
        if (!MainConfig.fermentTap.Value || !IsReady(fermenter, zdo))
        {
            return false;
        }
        Fermenter.ItemConversion conversion = fermenter.m_conversion.FirstOrDefault(c => c.m_from != null && c.m_from.gameObject.name.GetStableHashCode() == content);
        return conversion == null || conversion.m_to == null || !MainConfig.fermentToChests.Value
            || ChestIndex.HasRoom(at, range, conversion.m_to.gameObject, conversion.m_producedItems);
    }

    // Fills an empty fermenter with the first thing the chests have that it can ferment
    private static void Fill(Fermenter fermenter, ZNetView view, float range)
    {
        foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
        {
            if (conversion.m_from == null)
            {
                continue;
            }
            if (ChestIndex.Take(fermenter.transform.position, range, conversion.m_from.m_itemData.m_shared.m_name, 1) > 0)
            {
                view.InvokeRPC("RPC_AddItem", conversion.m_from.gameObject.name.GetStableHashCode(), false);
                return;
            }
        }
    }

    private static bool IsReady(Fermenter fermenter, ZDO zdo)
    {
        long started = zdo.GetLong(ZDOVars.s_startTime, 0L);
        return started != 0L && (ZNet.instance.GetTime() - new DateTime(started)).TotalSeconds > fermenter.m_fermentationDuration;
    }

    // Empties a ready fermenter the way tapping it does, but the drink goes into a chest. With no room for all of it
    // the fermenter is left as it is until there is (nothing is dropped).
    private static void Tap(Fermenter fermenter, ZDO zdo, int content, float range)
    {
        Fermenter.ItemConversion conversion = fermenter.m_conversion.FirstOrDefault(c => c.m_from != null && c.m_from.gameObject.name.GetStableHashCode() == content);
        if (conversion != null && conversion.m_to != null && MainConfig.fermentToChests.Value
            && !ChestIndex.RoomFor(fermenter.transform.position, range, conversion.m_to.gameObject, conversion.m_producedItems))
        {
            return;
        }

        zdo.Set(ZDOVars.s_content, 0);
        zdo.Set(ZDOVars.s_startTime, 0L);
        zdo.Set(ZDOVars.s_cheatedQueued, false);
        if (conversion == null || conversion.m_to == null)
        {
            return;
        }

        int left = conversion.m_producedItems;
        if (MainConfig.fermentToChests.Value)
        {
            left -= ChestIndex.Put(fermenter.transform.position, range, conversion.m_to.gameObject, left);
        }
        // With the setting off, the drink comes out of the tap, as it does for a player
        for (int i = 0; i < left; i++)
        {
            Vector3 at = fermenter.m_outputPoint.position + Vector3.up * 0.3f;
            ItemDrop.OnCreateNew(UnityEngine.Object.Instantiate(conversion.m_to, at, Quaternion.identity), false);
        }
    }

    private static ConfigEntry<bool> KindSetting(string prefabName, Piece piece)
    {
        if (kinds.TryGetValue(prefabName, out ConfigEntry<bool> setting))
        {
            return setting;
        }

        string pieceName = EnglishText.Of(piece.m_name, prefabName);
        setting = MainConfig.BindKind(KindSection, prefabName, pieceName, $"{pieceName} is filled from nearby chests, and what is ready is tapped into a chest.");
        kinds[prefabName] = setting;
        return setting;
    }
}
