using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Shield Generators: the shield generators you built are topped up with their fuel (black cores) from nearby chests.
internal static class ShieldGeneratorKeeper
{
    private const string KindSection = "Shield Generator Types";

    private static readonly HashSet<ShieldGenerator> generators = new HashSet<ShieldGenerator>();

    // The on/off setting of each kind of shield generator, by its prefab name
    private static readonly Dictionary<string, ConfigEntry<bool>> kinds = new Dictionary<string, ConfigEntry<bool>>();

    private static readonly AccessTools.FieldRef<ShieldGenerator, ZNetView> viewOf = AccessTools.FieldRefAccess<ShieldGenerator, ZNetView>("m_nview");

    public static void Track(ShieldGenerator generator)
    {
        generators.Add(generator);
    }

    public static void TrackExisting()
    {
        foreach (ShieldGenerator generator in UnityEngine.Object.FindObjectsByType<ShieldGenerator>(FindObjectsSortMode.None))
        {
            generators.Add(generator);
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
            ShieldGenerator generator = prefab.GetComponent<ShieldGenerator>();
            Piece piece = prefab.GetComponent<Piece>();
            if (generator != null && piece != null)
            {
                KindSetting(prefab.name, piece);
            }
        }
    }

    public static void DoRound()
    {
        generators.RemoveWhere(generator => generator == null);

        foreach (ShieldGenerator generator in generators.ToList())
        {
            try
            {
                Refuel(generator);
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not refuel the shield generator at {generator.transform.position}: {e.Message}");
            }
        }
    }

    private static void Refuel(ShieldGenerator generator)
    {
        ZNetView view = viewOf(generator);
        if (view == null || !view.IsValid() || generator.m_maxFuel <= 0 || generator.m_fuelItems.Count == 0)
        {
            return;
        }

        Piece piece = generator.GetComponent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer() || !KindSetting(Utils.GetPrefabName(generator.gameObject), piece).Value)
        {
            return;
        }

        // A generator that has never been fuelled holds its default amount
        float fuel = view.GetZDO().GetFloat(ZDOVars.s_fuel, generator.m_defaultFuel);
        float below = MainConfig.generatorRefillBelow.Value;
        if (below < 100f && fuel >= generator.m_maxFuel * below / 100f)
        {
            return;
        }
        int room = Mathf.FloorToInt(generator.m_maxFuel - fuel);
        if (room < 1)
        {
            return;
        }

        // Only the generator's owner fuels it, so two players never both fill the same one. One that another player
        // owns is claimed now and fuelled from the next round on.
        if (!Ownership.Hold(view))
        {
            return;
        }

        // Any of the fuels it takes, in the order the game lists them
        foreach (ItemDrop fuelItem in generator.m_fuelItems)
        {
            int got = ChestIndex.Take(generator.transform.position, MainConfig.generatorChestRange.Value, fuelItem.m_itemData.m_shared.m_name, room);
            for (int i = 0; i < got; i++)
            {
                view.InvokeRPC("RPC_AddFuel");
            }
            room -= got;
            if (room < 1)
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
        setting = MainConfig.BindKind(KindSection, prefabName, pieceName, $"{pieceName} is kept fuelled from nearby chests.");
        kinds[prefabName] = setting;
        return setting;
    }
}
