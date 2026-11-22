using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Splatform;
using UnityEngine;

namespace LazyGit;

// Auto Farming: a crop that has grown (the pickable a plant turns into) is harvested into a chest, and the same plant is
// planted again where it stood, with the seeds from the chests.
internal static class CropKeeper
{
    private const string KindSection = "Crop Types";

    private static readonly HashSet<Pickable> crops = new HashSet<Pickable>();

    // The crops by the prefab name of what grows: the plant that grows into each (its seeds are what planting costs).
    // Built from the game's own plants once the game's prefabs are known.
    private static Dictionary<string, GameObject> plantFor;

    // The on/off setting of each kind of crop, by its prefab name
    private static readonly Dictionary<string, ConfigEntry<bool>> kinds = new Dictionary<string, ConfigEntry<bool>>();

    private static readonly AccessTools.FieldRef<Pickable, ZNetView> viewOf = AccessTools.FieldRefAccess<Pickable, ZNetView>("m_nview");

    public static void Track(Pickable pickable)
    {
        if (IsCrop(pickable))
        {
            crops.Add(pickable);
        }
    }

    public static void TrackExisting()
    {
        foreach (Pickable pickable in UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None))
        {
            Track(pickable);
        }
        if (ZNetScene.instance != null)
        {
            ListKinds(ZNetScene.instance);
        }
    }

    // A pickable that a plant grows into (not a wild berry bush or mushroom), whose harvest is the product alone: it is
    // all put in a chest, so one with extra drops is left for the player
    private static bool IsCrop(Pickable pickable)
    {
        return pickable != null && pickable.m_itemPrefab != null && pickable.m_extraDrops.IsEmpty()
            && Plants().ContainsKey(Utils.GetPrefabName(pickable.gameObject));
    }

    private static Dictionary<string, GameObject> Plants()
    {
        if (plantFor != null || ZNetScene.instance == null)
        {
            return plantFor ?? new Dictionary<string, GameObject>();
        }

        plantFor = new Dictionary<string, GameObject>();
        foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
        {
            Plant plant = prefab != null ? prefab.GetComponent<Plant>() : null;
            if (plant == null || plant.m_attachDistance > 0f || prefab.GetComponent<Piece>() == null)
            {
                continue;
            }
            foreach (GameObject grown in plant.m_grownPrefabs)
            {
                if (grown != null && grown.GetComponent<Pickable>() != null && !plantFor.ContainsKey(grown.name))
                {
                    plantFor[grown.name] = prefab;
                }
            }
        }
        return plantFor;
    }

    public static void ListKinds(ZNetScene scene)
    {
        foreach (GameObject prefab in MainConfig.VanillaFirst(scene.m_prefabs))
        {
            Pickable pickable = prefab != null ? prefab.GetComponent<Pickable>() : null;
            if (pickable != null && IsCrop(pickable))
            {
                KindSetting(prefab.name, pickable);
            }
        }
    }

    public static void DoRound()
    {
        crops.RemoveWhere(crop => crop == null);

        foreach (Pickable crop in crops.ToList())
        {
            try
            {
                Harvest(crop);
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not harvest the crop at {crop.transform.position}: {e.Message}");
            }
        }
    }

    private static void Harvest(Pickable crop)
    {
        ZNetView view = viewOf(crop);
        if (view == null || !view.IsValid() || !crop.CanBePicked() || !KindSetting(Utils.GetPrefabName(crop.gameObject), crop).Value)
        {
            return;
        }

        Vector3 at = crop.transform.position;
        float range = MainConfig.cropChestRange.Value;
        if (!PrivateArea.CheckAccess(at, 0f, false) || !ChestIndex.HasRoom(at, range, crop.m_itemPrefab, 1))
        {
            return;   // (no chest with room: the crop is not claimed, and it waits)
        }

        // Only the crop's owner picks it. One that another player owns is claimed now and harvested from the next round.
        if (!Ownership.Hold(view))
        {
            return;
        }

        // What a player's click gives: the amount the world's settings scale it to, and a chance of more with farming skill
        Player me = Player.m_localPlayer;
        int amount = crop.m_dontScale ? crop.m_amount : Mathf.Max(crop.m_minAmountScaled, Game.instance.ScaleDrops(crop.m_itemPrefab, crop.m_amount));
        if (crop.m_pickRaiseSkill != Skills.SkillType.None && UnityEngine.Random.value < me.GetSkillFactor(crop.m_pickRaiseSkill) * crop.m_maxLevelBonusChance)
        {
            amount += crop.m_bonusYieldAmount;
        }

        // All of it goes into the chests, or the crop stays as it is
        if (!ChestIndex.RoomFor(at, range, crop.m_itemPrefab, amount))
        {
            return;
        }
        ChestIndex.Put(at, range, crop.m_itemPrefab, amount);

        if (crop.m_harvestStat != PlayerStatType.None)
        {
            Game.instance.IncrementPlayerStat(crop.m_harvestStat);
        }
        Game.instance.GetPlayerProfile().IncrementStatPickable(crop.m_itemPrefab.name);
        if (crop.m_pickRaiseSkill != Skills.SkillType.None)
        {
            me.RaiseSkill(crop.m_pickRaiseSkill);
        }
        crop.m_pickEffector.Create(at, Quaternion.identity, null, 1f, -1, me.GetZDOID());

        // What is needed to plant it again is worked out before the crop goes: a crop that comes back by itself (a
        // bush) is not planted again
        GameObject plant = null;
        if (MainConfig.cropReplant.Value && crop.m_respawnTimeMinutes <= 0f && crop.m_hideWhenPicked == null)
        {
            Plants().TryGetValue(Utils.GetPrefabName(crop.gameObject), out plant);
        }

        // The same call a player's click ends with: the crop is picked, and goes
        view.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true);

        if (plant != null)
        {
            Replant(plant, at, range, me);
        }
    }

    // Plants the same plant where the crop stood, for the seeds a player pays, which are taken from the chests. With no
    // seeds in the chests the spot is left empty.
    private static void Replant(GameObject plant, Vector3 at, float range, Player me)
    {
        Piece piece = plant.GetComponent<Piece>();
        List<KeyValuePair<string, int>> cost = piece.m_resources
            .Where(r => r.m_resItem != null && r.m_amount > 0)
            .Select(r => new KeyValuePair<string, int>(r.m_resItem.m_itemData.m_shared.m_name, r.m_amount))
            .ToList();
        if (!ChestIndex.TakeAll(at, range, cost))
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f);
        GameObject planted = UnityEngine.Object.Instantiate(plant, at, rotation);
        Piece placed = planted.GetComponent<Piece>();
        if (placed != null)
        {
            placed.SetCreator(me.GetPlayerID(), PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
        }
        piece.m_placeEffect.Create(at, rotation, planted.transform, 1f, -1, me.GetZDOID());
    }

    private static ConfigEntry<bool> KindSetting(string prefabName, Pickable crop)
    {
        if (kinds.TryGetValue(prefabName, out ConfigEntry<bool> setting))
        {
            return setting;
        }

        string itemName = crop.m_itemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
        string cropName = EnglishText.Of(itemName, prefabName);
        setting = MainConfig.BindKind(KindSection, prefabName, cropName, $"{cropName} is harvested into a chest when it has grown, and planted again from the seeds in the chests.");
        kinds[prefabName] = setting;
        return setting;
    }
}
