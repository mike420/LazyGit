using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Tidy: items lying on the ground beside your smelters, kilns, cooking stations and fermenters (what they turn out)
// are put in a chest. Only stackable things (ore, bars, food, drinks...) that nobody dropped on purpose.
internal static class TidyKeeper
{
    private static readonly HashSet<ItemDrop> items = new HashSet<ItemDrop>();

    private static readonly AccessTools.FieldRef<ItemDrop, float> spawnTimeOf = AccessTools.FieldRefAccess<ItemDrop, float>("m_spawnTime");

    // So that a big pile is cleared over a few rounds
    private const int MostPerRound = 20;

    public static void Track(ItemDrop item)
    {
        items.Add(item);
    }

    public static void TrackExisting()
    {
        foreach (ItemDrop item in UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
        {
            items.Add(item);
        }
    }

    public static void DoRound()
    {
        items.RemoveWhere(item => item == null);
        if (items.Count == 0)
        {
            return;
        }

        // The places things come out: the stations the player built
        List<Vector3> stations = StationKeeper.Positions().Concat(CookingKeeper.Positions()).Concat(FermentKeeper.Positions()).ToList();
        if (stations.Count == 0)
        {
            return;
        }

        float near = MainConfig.tidyStationDistance.Value;
        float nearSqr = near * near;
        float wait = MainConfig.tidyWait.Value;
        float range = MainConfig.tidyChestRange.Value;
        int tidied = 0;

        foreach (ItemDrop item in items.ToList())
        {
            try
            {
                if (Tidy(item, stations, nearSqr, wait, range) && ++tidied >= MostPerRound)
                {
                    break;
                }
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not tidy the item at {item.transform.position}: {e.Message}");
            }
        }
    }

    // Returns whether the item was put in a chest
    private static bool Tidy(ItemDrop item, List<Vector3> stations, float nearSqr, float wait, float range)
    {
        // The cheap checks first. An item the player dropped has its auto pickup switched off by the game, and so has an
        // egg that is being left to hatch: neither is touched. Anything that is not stackable (a weapon with its quality
        // and wear, say) is left too.
        ZNetView view = item.GetComponent<ZNetView>();
        if (!item.m_autoPickup || view == null || !view.IsValid() || item.IsPiece() || item.m_itemData?.m_shared == null
            || item.m_itemData.m_shared.m_maxStackSize <= 1 || Time.time - spawnTimeOf(item) < wait)
        {
            return false;
        }

        Vector3 at = item.transform.position;
        if (!stations.Any(s => (s - at).sqrMagnitude <= nearSqr) || item.InTar())
        {
            return false;
        }

        item.Load();   // (what the item holds, as the game's own pickup reads it)
        if (!ChestIndex.HasRoomFor(at, range, item.m_itemData))
        {
            return false;
        }

        // Only the item's owner removes it. One that another player owns is claimed now and tidied from the next round.
        if (!Ownership.Hold(view))
        {
            return false;
        }

        // It leaves the ground only once it is in a chest
        if (!ChestIndex.PutStack(at, range, item.m_itemData))
        {
            return false;
        }
        view.Destroy();
        return true;
    }
}
