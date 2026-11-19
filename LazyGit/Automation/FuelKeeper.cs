using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Fuel: the fires and torches the player built take their fuel from nearby chests.
internal static class FuelKeeper
{
    private const string KindSection = "Fire Types";

    private static readonly HashSet<Fireplace> fires = new HashSet<Fireplace>();

    // The on/off setting of each kind of fire, by its prefab name
    private static readonly Dictionary<string, ConfigEntry<bool>> kinds = new Dictionary<string, ConfigEntry<bool>>();

    private static readonly AccessTools.FieldRef<Fireplace, ZNetView> viewOf = AccessTools.FieldRefAccess<Fireplace, ZNetView>("m_nview");

    public static void Track(Fireplace fire)
    {
        fires.Add(fire);
    }

    public static void TrackExisting()
    {
        foreach (Fireplace fire in Object.FindObjectsByType<Fireplace>(FindObjectsSortMode.None))
        {
            fires.Add(fire);
        }
        if (ZNetScene.instance != null)
        {
            ListFireKinds(ZNetScene.instance);
        }
    }

    // One setting for every piece that burns fuel, so each kind can be switched off on its own
    public static void ListFireKinds(ZNetScene scene)
    {
        foreach (GameObject prefab in MainConfig.VanillaFirst(scene.m_prefabs))
        {
            if (prefab == null)
            {
                continue;
            }
            Fireplace fire = prefab.GetComponent<Fireplace>();
            Piece piece = prefab.GetComponent<Piece>();
            if (piece != null && TakesFuel(fire))
            {
                KindSetting(prefab.name, piece, fire);
            }
        }
    }

    public static void DoRound()
    {
        fires.RemoveWhere(fire => fire == null);

        Dictionary<string, int> fed = new Dictionary<string, int>();
        foreach (Fireplace fire in fires.ToList())
        {
            try
            {
                int amount = Feed(fire);
                if (amount > 0)
                {
                    string fuel = fire.m_fuelItem.m_itemData.m_shared.m_name;
                    fed[fuel] = (fed.TryGetValue(fuel, out int before) ? before : 0) + amount;
                }
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not feed the fire at {fire.transform.position}: {e.Message}");
            }
        }

        if (fed.Count > 0 && MainConfig.fuelMessage.Value && Player.m_localPlayer != null)
        {
            string what = string.Join(", ", fed.Select(f => $"{f.Value} {Localization.instance.Localize(f.Key).ToLowerInvariant()}"));
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, Translations.T("lazygit_fuel_refilled", what));
        }
    }

    // Fills one fire if it is one of the player's, it is running low and fuel can be found. Returns the fuel put in.
    private static int Feed(Fireplace fire)
    {
        if (!TakesFuel(fire))
        {
            return 0;
        }

        ZNetView view = viewOf(fire);
        if (view == null || !view.IsValid())
        {
            return 0;
        }

        // Wild fires (villages, dungeons) are not the player's to look after
        Piece piece = fire.GetComponent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer() || !KindSetting(Utils.GetPrefabName(fire.gameObject), piece, fire).Value)
        {
            return 0;
        }

        float fuel = view.GetZDO().GetFloat(ZDOVars.s_fuel, 0f);
        float below = MainConfig.fuelRefillBelow.Value;
        if (below < 100f && fuel >= fire.m_maxFuel * below / 100f)
        {
            return 0;
        }
        int room = Mathf.FloorToInt(fire.m_maxFuel - fuel);
        if (room < 1)
        {
            return 0;
        }

        // Only the fire's owner feeds it, so two players never both fill the same fire. One that another player owns
        // is claimed now and fed from the next round on.
        if (!Ownership.Hold(view))
        {
            return 0;
        }

        string fuelName = fire.m_fuelItem.m_itemData.m_shared.m_name;
        int got = ChestIndex.Take(fire.transform.position, MainConfig.fuelChestRange.Value, fuelName, room);
        if (got < room && MainConfig.fuelFromInventory.Value)
        {
            got += TakeFromPlayer(fuelName, room - got);
        }
        if (got > 0)
        {
            view.InvokeRPC("RPC_AddFuelAmount", (float)got);
        }
        return got;
    }

    private static int TakeFromPlayer(string itemName, int amount)
    {
        Inventory bag = Player.m_localPlayer.GetInventory();
        int take = Mathf.Min(bag.CountItems(itemName, -1, false), amount);
        if (take > 0)
        {
            bag.RemoveItem(itemName, take, -1, false);
        }
        return take;
    }

    // A fire that burns fuel which can be put in (not one that burns forever, or that cannot be filled)
    private static bool TakesFuel(Fireplace fire)
    {
        return fire != null && !fire.m_infiniteFuel && fire.m_canRefill && fire.m_fuelItem != null;
    }

    private static ConfigEntry<bool> KindSetting(string prefabName, Piece piece, Fireplace fire)
    {
        if (kinds.TryGetValue(prefabName, out ConfigEntry<bool> setting))
        {
            return setting;
        }

        string pieceName = EnglishText.Of(piece.m_name, prefabName);
        string fuelName = EnglishText.Of(fire.m_fuelItem.m_itemData.m_shared.m_name, "its fuel").ToLowerInvariant();
        setting = MainConfig.BindKind(KindSection, prefabName, pieceName, $"{pieceName} takes {fuelName} from nearby chests.");
        kinds[prefabName] = setting;
        return setting;
    }
}
