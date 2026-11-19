using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Ballista: ballistas and other turrets are restocked with bolts from nearby chests.
internal static class BallistaKeeper
{
    private const string KindSection = "Ballista Types";

    private static readonly HashSet<Turret> turrets = new HashSet<Turret>();

    // The on/off setting of each kind of turret, by its prefab name
    private static readonly Dictionary<string, ConfigEntry<bool>> kinds = new Dictionary<string, ConfigEntry<bool>>();

    private static readonly AccessTools.FieldRef<Turret, ZNetView> viewOf = AccessTools.FieldRefAccess<Turret, ZNetView>("m_nview");

    public static void Track(Turret turret)
    {
        turrets.Add(turret);
    }

    public static void TrackExisting()
    {
        foreach (Turret turret in UnityEngine.Object.FindObjectsByType<Turret>(FindObjectsSortMode.None))
        {
            turrets.Add(turret);
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
            Turret turret = prefab.GetComponent<Turret>();
            Piece piece = prefab.GetComponent<Piece>();
            if (turret != null && piece != null && turret.m_maxAmmo > 0)
            {
                KindSetting(prefab.name, piece);
            }
        }
    }

    public static void DoRound()
    {
        turrets.RemoveWhere(turret => turret == null);

        foreach (Turret turret in turrets.ToList())
        {
            try
            {
                Restock(turret);
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not restock the ballista at {turret.transform.position}: {e.Message}");
            }
        }
    }

    private static void Restock(Turret turret)
    {
        // A turret that never runs out has nothing to restock
        if (turret.m_maxAmmo <= 0)
        {
            return;
        }

        ZNetView view = viewOf(turret);
        if (view == null || !view.IsValid())
        {
            return;
        }

        Piece piece = turret.GetComponent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer() || !KindSetting(Utils.GetPrefabName(turret.gameObject), piece).Value)
        {
            return;
        }

        ZDO zdo = view.GetZDO();
        int have = zdo.GetInt(ZDOVars.s_ammo, 0);
        float below = MainConfig.ballistaRefillBelow.Value;
        if (below < 100f && have >= turret.m_maxAmmo * below / 100f)
        {
            return;
        }
        int room = turret.m_maxAmmo - have;
        if (room < 1)
        {
            return;
        }

        // Only the turret's owner loads it, so two players never both fill the same one. One that another player
        // owns is claimed now and loaded from the next round on.
        if (!Ownership.Hold(view))
        {
            return;
        }

        // A loaded turret keeps the kind of bolt it holds; an empty one takes whichever kind the chests have
        List<ItemDrop> kindsToTry = new List<ItemDrop>();
        if (have > 0 || turret.m_defaultAmmo != null)
        {
            string loaded = turret.GetAmmoType();
            ItemDrop kind = turret.m_allowedAmmo.Select(a => a.m_ammo).FirstOrDefault(a => a != null && a.name == loaded);
            if (kind == null)
            {
                return;
            }
            kindsToTry.Add(kind);
        }
        else
        {
            kindsToTry.AddRange(turret.m_allowedAmmo.Select(a => a.m_ammo).Where(a => a != null));
        }

        foreach (ItemDrop kind in kindsToTry)
        {
            int got = ChestIndex.Take(turret.transform.position, MainConfig.ballistaChestRange.Value, kind.m_itemData.m_shared.m_name, room);
            for (int i = 0; i < got; i++)
            {
                view.InvokeRPC("RPC_AddAmmo", kind.name);
            }
            if (got > 0)
            {
                return;
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
        setting = MainConfig.BindKind(KindSection, prefabName, pieceName, $"{pieceName} is restocked with ammunition from nearby chests.");
        kinds[prefabName] = setting;
        return setting;
    }
}
