using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// The chests that automatic chores may take things from, and put things into. Every chest in the world is listed as it
// wakes up; once a round, the ones that are fair game are picked out, and a chore asks for the ones near it.
internal static class ChestIndex
{
    private static readonly HashSet<Container> everyChest = new HashSet<Container>();

    private static readonly AccessTools.FieldRef<Container, ZNetView> viewOf = AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");
    private static readonly AccessTools.FieldRef<Container, Piece> pieceOf = AccessTools.FieldRefAccess<Container, Piece>("m_piece");

    // The fair-game chests of this round, made the first time a chore asks for them (most rounds, nothing needs a
    // chest): the ones the local player owns, which can be changed now, and the ones other players own
    private static List<Container> ownedThisRound;
    private static List<Container> othersThisRound;

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
        ownedThisRound = null;
        othersThisRound = null;
        everyChest.RemoveWhere(chest => chest == null);
    }

    // Takes up to 'amount' of an item (by its shared name) from the chests within 'range' of 'at', nearest first.
    // Returns how many were taken.
    public static int Take(Vector3 at, float range, string itemName, int amount)
    {
        float maxSqr = range * range;
        int taken = 0;
        foreach (Container chest in InRange(Owned(), at, maxSqr))
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

        // Another player's chest that has what is needed is claimed, and used from a later round on
        if (taken < amount)
        {
            foreach (Container chest in InRange(Others(), at, maxSqr).Where(c => c.GetInventory().CountItems(itemName, -1, false) > 0))
            {
                Ownership.Hold(viewOf(chest));
            }
        }
        return taken;
    }

    // Whether any chest within 'range' of 'at' holds the item (its shared name), the player's own or another's. Nothing
    // is taken or claimed: a chore asks this before it claims a piece, so that it is claimed only when there is work.
    public static bool Has(Vector3 at, float range, string itemName)
    {
        float maxSqr = range * range;
        return InRange(Owned(), at, maxSqr).Concat(InRange(Others(), at, maxSqr))
            .Any(c => c.GetInventory().CountItems(itemName, -1, false) > 0);
    }

    // Whether the chests within 'range' of 'at', the player's own and other players', have room for 'amount' of an item
    // (its prefab) between them. Nothing is put in or claimed (see Has).
    public static bool HasRoom(Vector3 at, float range, GameObject prefab, int amount)
    {
        ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (drop == null)
        {
            return false;
        }
        ItemDrop.ItemData item = AsAdded(drop);
        float maxSqr = range * range;
        int room = 0;
        foreach (Container chest in InRange(Owned(), at, maxSqr).Concat(InRange(Others(), at, maxSqr)))
        {
            Inventory inventory = chest.GetInventory();
            room += inventory.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel) + inventory.GetEmptySlots() * item.m_shared.m_maxStackSize;
            if (room >= amount)
            {
                return true;
            }
        }
        return false;
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
        ItemDrop.ItemData probe = AsAdded(drop);
        float maxSqr = range * range;
        int put = 0;
        foreach (Container chest in InRange(Owned(), at, maxSqr)
            .OrderBy(c => c.GetInventory().CountItems(itemName, -1, false) > 0 ? 0 : 1))
        {
            Inventory inventory = chest.GetInventory();
            while (put < amount && inventory.CanAddItem(probe, 1) && inventory.AddItem(prefab, 1))
            {
                put++;
            }
            if (put >= amount)
            {
                break;
            }
        }

        if (put < amount)
        {
            ClaimOthersWithRoom(at, maxSqr, prefab);
        }
        return put;
    }

    // Whether the chests within 'range' of 'at' have room for 'amount' of an item (its prefab), all of it. Nothing is
    // put in. A chore that must not drop anything on the ground asks this first, and waits if there is no room.
    public static bool RoomFor(Vector3 at, float range, GameObject prefab, int amount)
    {
        ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (drop == null)
        {
            return false;
        }
        ItemDrop.ItemData item = AsAdded(drop);
        float maxSqr = range * range;
        int room = 0;
        foreach (Container chest in InRange(Owned(), at, maxSqr))
        {
            Inventory inventory = chest.GetInventory();
            room += inventory.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel) + inventory.GetEmptySlots() * item.m_shared.m_maxStackSize;
            if (room >= amount)
            {
                return true;
            }
        }
        ClaimOthersWithRoom(at, maxSqr, prefab);
        return false;
    }

    // The item as Inventory.AddItem(prefab) makes it: stamped with the world level. The game's own room checks for a
    // prefab look for stack space at world level 0, so in a higher world they would not see the stacks it adds to.
    private static ItemDrop.ItemData AsAdded(ItemDrop drop)
    {
        ItemDrop.ItemData item = drop.m_itemData.Clone();
        item.m_worldLevel = (byte)Game.m_worldLevel;
        return item;
    }

    // Another player's chest with room is claimed, and used from a later round on
    private static void ClaimOthersWithRoom(Vector3 at, float maxSqr, GameObject prefab)
    {
        ItemDrop.ItemData probe = AsAdded(prefab.GetComponent<ItemDrop>());
        foreach (Container chest in InRange(Others(), at, maxSqr).Where(c => c.GetInventory().CanAddItem(probe, 1)))
        {
            Ownership.Hold(viewOf(chest));
        }
    }

    // The chests of a list within range, nearest first
    private static IEnumerable<Container> InRange(List<Container> chests, Vector3 at, float maxSqr)
    {
        return chests
            .Select(c => (chest: c, sqr: (c.transform.position - at).sqrMagnitude))
            .Where(c => c.sqr <= maxSqr)
            .OrderBy(c => c.sqr)
            .Select(c => c.chest)
            .ToList();
    }

    private static List<Container> Owned()
    {
        if (ownedThisRound == null)
        {
            Pick();
        }
        return ownedThisRound;
    }

    private static List<Container> Others()
    {
        if (othersThisRound == null)
        {
            Pick();
        }
        return othersThisRound;
    }

    private static void Pick()
    {
        ownedThisRound = new List<Container>();
        othersThisRound = new List<Container>();
        long me = Player.m_localPlayer.GetPlayerID();
        foreach (Container chest in everyChest)
        {
            if (chest == null || !IsFairGame(chest, me))
            {
                continue;
            }

            // Only the player who owns a chest in the network may change what is in it
            (viewOf(chest).IsOwner() ? ownedThisRound : othersThisRound).Add(chest);
        }
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
        if (view == null || !view.IsValid() || chest.GetInventory() == null)
        {
            return false;
        }

        // Whether a chest is open: its owner knows it itself; for another player's chest the game keeps it in the
        // chest's data (the local flag is only ever set on the owner's side)
        return view.IsOwner() ? !chest.IsInUse() : view.GetZDO().GetInt(ZDOVars.s_inUse, 0) != 1;
    }
}
