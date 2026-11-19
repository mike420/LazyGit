using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// The chests that automatic chores may take things from. Every chest in the world is listed as it wakes up; once a
// round, the ones that are fair game are picked out, and a chore asks for the ones near it.
internal static class ChestIndex
{
    private static readonly HashSet<Container> everyChest = new HashSet<Container>();

    private static readonly AccessTools.FieldRef<Container, ZNetView> viewOf = AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");
    private static readonly AccessTools.FieldRef<Container, Piece> pieceOf = AccessTools.FieldRefAccess<Container, Piece>("m_piece");

    // The usable chests of this round, made the first time a chore asks for them (most rounds, nothing needs a chest)
    private static List<Container> usableThisRound;

    public static void Track(Container chest)
    {
        everyChest.Add(chest);
    }

    public static void TrackExisting()
    {
        foreach (Container chest in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
        {
            everyChest.Add(chest);
        }
    }

    public static void NewRound()
    {
        usableThisRound = null;
        everyChest.RemoveWhere(chest => chest == null);
    }

    // Takes up to 'amount' of an item (by its shared name) from the chests within 'range' of 'at', nearest first.
    // Returns how many were taken.
    public static int Take(Vector3 at, float range, string itemName, int amount)
    {
        float maxSqr = range * range;
        int taken = 0;
        foreach (Container chest in Usable()
            .Select(c => (chest: c, sqr: (c.transform.position - at).sqrMagnitude))
            .Where(c => c.sqr <= maxSqr)
            .OrderBy(c => c.sqr)
            .Select(c => c.chest))
        {
            Inventory inventory = chest.GetInventory();
            int have = inventory.CountItems(itemName, -1, false);
            if (have <= 0)
            {
                continue;
            }
            int take = Mathf.Min(have, amount - taken);
            inventory.RemoveItem(itemName, take, -1, false);
            taken += take;
            if (taken >= amount)
            {
                break;
            }
        }
        return taken;
    }

    // Puts up to 'amount' of an item (its prefab) into the chests within 'range' of 'at'. A chest that already holds
    // the item is used first, then the nearest. Returns how many were put in; what does not fit is left with the caller.
    public static int Put(Vector3 at, float range, GameObject prefab, int amount)
    {
        ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (drop == null)
        {
            return 0;
        }
        string itemName = drop.m_itemData.m_shared.m_name;
        float maxSqr = range * range;
        int put = 0;
        foreach (Container chest in Usable()
            .Select(c => (chest: c, sqr: (c.transform.position - at).sqrMagnitude))
            .Where(c => c.sqr <= maxSqr)
            .OrderBy(c => c.chest.GetInventory().CountItems(itemName, -1, false) > 0 ? 0 : 1)
            .ThenBy(c => c.sqr)
            .Select(c => c.chest))
        {
            Inventory inventory = chest.GetInventory();
            while (put < amount && inventory.CanAddItem(prefab, 1) && inventory.AddItem(prefab, 1))
            {
                put++;
            }
            if (put >= amount)
            {
                break;
            }
        }
        return put;
    }

    private static List<Container> Usable()
    {
        return usableThisRound ??= PickUsable();
    }

    private static List<Container> PickUsable()
    {
        List<Container> picked = new List<Container>();
        long me = Player.m_localPlayer.GetPlayerID();
        foreach (Container chest in everyChest)
        {
            if (chest == null || !IsFairGame(chest, me))
            {
                continue;
            }

            // Only the player who owns a chest in the network may change what is in it. Another player's chest is
            // claimed now and used from the next round on.
            ZNetView view = viewOf(chest);
            if (!view.IsOwner())
            {
                view.ClaimOwnership();
                continue;
            }
            picked.Add(chest);
        }
        return picked;
    }

    // A chest somebody built and is free to use: not a cart, a ship or a gravestone, not another player's private
    // chest, not a guild one, not behind a ward the player has no access to, and not one that is open right now
    private static bool IsFairGame(Container chest, long me)
    {
        if (chest.m_wagon != null || chest.GetComponentInParent<Ship>() != null || chest.GetComponent<TombStone>() != null)
        {
            return false;
        }

        Piece piece = pieceOf(chest);
        if (piece == null || !piece.IsPlacedByPlayer())
        {
            return false;
        }
        switch (chest.m_privacy)
        {
            case Container.PrivacySetting.Public:
                break;
            case Container.PrivacySetting.Private when piece.GetCreator() == me:
                break;
            default:
                return false;
        }
        if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, false))
        {
            return false;
        }

        ZNetView view = viewOf(chest);
        return view != null && view.IsValid() && chest.GetInventory() != null && !chest.IsInUse();
    }
}
