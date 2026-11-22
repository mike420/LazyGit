using System.Collections.Generic;

namespace LazyGit;

// Auto Egg Pickup: chicken and Asksvin eggs are picked up by walking close to them, like other items on the ground are
// when the game's auto pickup is on. Warm eggs (the ones that are hatching) can be left alone.
internal static class EggKeeper
{
    private static readonly HashSet<string> eggPrefabs = new HashSet<string> { "ChickenEgg", "AsksvinEgg" };

    // The eggs that are on the ground but kept from the auto pickup for now: warm ones, and new ones until the egg has
    // looked at itself once (the first look is 5 to 10 seconds after it appears), so a warm egg is never taken first.
    private static readonly HashSet<ItemDrop> held = new HashSet<ItemDrop>();

    private static bool IsEgg(ItemDrop drop)
    {
        return drop != null && drop.m_itemData != null && drop.m_itemData.m_dropPrefab != null
            && eggPrefabs.Contains(drop.m_itemData.m_dropPrefab.name);
    }

    private static bool Active
    {
        get { return MainConfig.modEnabled.Value && MainConfig.eggEnabled.Value; }
    }

    // The game only hatches an egg that has a start time (EggGrow.GrowUpdate sets it while the egg can grow, and clears
    // it when it cannot); the game's own hover text calls an egg with one "warm"
    private static bool IsWarm(ZNetView view)
    {
        return view != null && view.IsValid() && view.GetZDO().GetFloat(ZDOVars.s_growStart) > 0f;
    }

    // After ItemDrop.Awake of any item
    public static void OnItemAwake(ItemDrop drop)
    {
        if (!Active || !IsEgg(drop))
        {
            return;
        }

        drop.m_autoPickup = true;
        if (MainConfig.eggLeaveWarm.Value && drop.GetComponent<EggGrow>() != null)
        {
            drop.m_autoPickup = false;
            held.Add(drop);
        }
    }

    // After EggGrow.GrowUpdate: the egg has just worked out whether it is growing
    public static void OnGrowUpdate(EggGrow egg)
    {
        if (!Active || egg == null)
        {
            return;
        }

        ItemDrop drop = egg.GetComponent<ItemDrop>();
        if (!IsEgg(drop))
        {
            return;
        }

        held.RemoveWhere(d => d == null);
        bool leave = MainConfig.eggLeaveWarm.Value && IsWarm(drop.GetComponent<ZNetView>());
        if (leave && drop.m_autoPickup)
        {
            drop.m_autoPickup = false;
            held.Add(drop);
        }
        else if (!leave && held.Remove(drop))
        {
            drop.m_autoPickup = true;
        }
    }

    // After ItemDrop.OnPlayerDrop: an egg the player dropped is not picked up again at once; leave it that way
    public static void OnPlayerDrop(ItemDrop drop)
    {
        held.Remove(drop);
    }

    // The settings changed: eggs kept back are let go if they should no longer be
    public static void Reapply()
    {
        held.RemoveWhere(d => d == null);
        if (Active && MainConfig.eggLeaveWarm.Value)
        {
            return;
        }
        foreach (ItemDrop drop in held)
        {
            drop.m_autoPickup = true;
        }
        held.Clear();
    }
}
