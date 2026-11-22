using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Collect: beehives (honey) and sap extractors (sap) are emptied into a chest when they are full.
internal static class CollectKeeper
{
    private static readonly HashSet<Beehive> hives = new HashSet<Beehive>();
    private static readonly HashSet<SapCollector> extractors = new HashSet<SapCollector>();

    private static readonly AccessTools.FieldRef<Beehive, ZNetView> hiveView = AccessTools.FieldRefAccess<Beehive, ZNetView>("m_nview");
    private static readonly AccessTools.FieldRef<SapCollector, ZNetView> extractorView = AccessTools.FieldRefAccess<SapCollector, ZNetView>("m_nview");

    public static void Track(Beehive hive)
    {
        hives.Add(hive);
    }

    public static void Track(SapCollector extractor)
    {
        extractors.Add(extractor);
    }

    public static void TrackExisting()
    {
        foreach (Beehive hive in UnityEngine.Object.FindObjectsByType<Beehive>(FindObjectsSortMode.None))
        {
            hives.Add(hive);
        }
        foreach (SapCollector extractor in UnityEngine.Object.FindObjectsByType<SapCollector>(FindObjectsSortMode.None))
        {
            extractors.Add(extractor);
        }
    }

    public static void DoRound()
    {
        hives.RemoveWhere(hive => hive == null);
        extractors.RemoveWhere(extractor => extractor == null);

        if (MainConfig.collectHoney.Value)
        {
            foreach (Beehive hive in hives.ToList())
            {
                Guarded(hive.transform, () => Empty(hive.gameObject, hiveView(hive), hive.m_honeyItem, hive.m_maxHoney));
            }
        }
        if (MainConfig.collectSap.Value)
        {
            foreach (SapCollector extractor in extractors.ToList())
            {
                Guarded(extractor.transform, () => Empty(extractor.gameObject, extractorView(extractor), extractor.m_spawnItem, extractor.m_maxLevel));
            }
        }
    }

    private static void Guarded(Transform where, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not collect at {where.position}: {e.Message}");
        }
    }

    // Takes what has built up, as a player's click does, but into a chest. What no chest has room for stays where it is.
    private static void Empty(GameObject piece, ZNetView view, ItemDrop product, int most)
    {
        if (view == null || !view.IsValid() || product == null)
        {
            return;
        }

        ZDO zdo = view.GetZDO();
        int level = zdo.GetInt(ZDOVars.s_level, 0);
        if (level < 1 || level < most * MainConfig.collectAt.Value / 100f)
        {
            return;
        }

        Piece built = piece.GetComponent<Piece>();
        if (built == null || !built.IsPlacedByPlayer())
        {
            return;
        }

        // Only a piece that a chest has room for the product of is claimed
        int perUnit = Mathf.Max(1, Game.instance.ScaleDrops(product.m_itemData, 1));
        if (!ChestIndex.HasRoom(piece.transform.position, MainConfig.collectChestRange.Value, product.gameObject, 1))
        {
            return;
        }

        // Only the owner changes what the piece holds. One that another player owns is claimed now and emptied from
        // the next round on.
        if (!Ownership.Hold(view))
        {
            return;
        }

        int put = ChestIndex.Put(piece.transform.position, MainConfig.collectChestRange.Value, product.gameObject, level * perUnit);
        if (put < 1)
        {
            return;
        }

        int unitsTaken = Mathf.Min(level, Mathf.CeilToInt(put / (float)perUnit));
        zdo.Set(ZDOVars.s_level, level - unitsTaken);
        if (level - unitsTaken == 0 && piece.GetComponent<SapCollector>() != null)
        {
            view.InvokeRPC(ZNetView.Everybody, "RPC_UpdateEffects");
        }
    }
}
