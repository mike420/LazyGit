using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LazyGit;

// Auto Replant: when a tree stump (or a tree that has no stump) is destroyed, a sapling of the same kind is planted
// where it stood, after a short wait. Stumps can also be made to go away on their own.
internal static class TreeKeeper
{
    // What each tree leaves behind (by prefab name), and the saplings that grow into it. The first sapling the game
    // knows is used, so saplings added by other mods are used when they are there.
    private static readonly Dictionary<string, string[]> saplingFor = new Dictionary<string, string[]>
    {
        ["Beech_Stub"] = new[] { "Beech_Sapling" },
        ["Beech1_Stub"] = new[] { "Beech_Sapling" },
        ["FirTree_Stub"] = new[] { "FirTree_Sapling" },
        ["Pinetree_01_Stub"] = new[] { "PineTree_Sapling" },
        ["BirchStub"] = new[] { "Birch_Sapling" },
        ["OakStub"] = new[] { "Oak_Sapling" },
        ["SwampTree1_Stub"] = new[] { "Ancient_Sapling", "ancient_sapling" },
        ["YggaShoot_small1"] = new[] { "YggaShoot_small1" },
        ["YggaShoot1"] = new[] { "Ygga_Sapling" },
        ["YggaShoot2"] = new[] { "Ygga_Sapling", "YggaShoot2" },
        ["YggaShoot3"] = new[] { "Ygga_Sapling" },
        ["AshlandsTree1"] = new[] { "Ashwood_Sapling" },
        ["AshlandsTree3"] = new[] { "Ashwood_Sapling" },
        ["AshlandsTree4"] = new[] { "Ashwood_Sapling" },
        ["AshlandsTree5"] = new[] { "Ashwood_Sapling" },
        ["AshlandsTree6"] = new[] { "Ashwood_Sapling" },
        ["AshlandsTree6_big"] = new[] { "Ashwood_Sapling" },
        ["Pinetree_Snow"] = new[] { "FirTree_big_Sapling" },
        ["Pinetree_Snow_Stub"] = new[] { "FirTree_big_Sapling" },
        ["SnowFirTree"] = new[] { "FirTree_big_Sapling" },
    };

    // A stump's lifetime as the game made it, so that it comes back if the setting is switched off
    private static readonly Dictionary<Destructible, float> originalLifetime = new Dictionary<Destructible, float>();

    private const float RemovedStumpSeconds = 0.01f;

    // Called as a Destructible is destroyed. Only its owner plants: the owner is one player, so one sapling is planted.
    public static void OnDestroyed(Destructible destructible)
    {
        if (!MainConfig.modEnabled.Value || !MainConfig.replantEnabled.Value || destructible == null || ZNetScene.instance == null)
        {
            return;
        }

        ZNetView view = destructible.GetComponent<ZNetView>();
        if (view == null || !view.IsValid() || !view.IsOwner())
        {
            return;
        }

        string tree = Utils.GetPrefabName(destructible.gameObject);
        if (!saplingFor.TryGetValue(tree, out string[] saplings))
        {
            return;
        }
        if (!MainConfig.replantSwamp.Value && tree.StartsWith("SwampTree1"))
        {
            return;
        }

        Vector3 where = destructible.transform.position;
        float clear = MainConfig.replantWorkbenchClearance.Value;
        if (clear > 0f && CraftingStation.FindClosestStationInRange("$piece_workbench", where, clear) != null)
        {
            return;
        }
        if (Random.value * 100f >= MainConfig.replantChance.Value)
        {
            return;
        }

        GameObject sapling = MainConfig.replantRandom.Value ? AnySapling() : FirstKnown(saplings);
        if (sapling != null && LazyGitMod.Instance != null)
        {
            LazyGitMod.Instance.StartCoroutine(Plant(sapling, where));
        }
    }

    // Before a felled tree leaves its stump: the stump is made to disappear on its own (or left as the game has it)
    public static void SetStumpLifetime(TreeBase tree)
    {
        if (tree == null || tree.m_stubPrefab == null)
        {
            return;
        }
        Destructible stump = tree.m_stubPrefab.GetComponent<Destructible>();
        if (stump == null)
        {
            return;
        }

        if (!originalLifetime.ContainsKey(stump))
        {
            originalLifetime[stump] = stump.m_ttl;
        }
        bool remove = MainConfig.modEnabled.Value && MainConfig.replantRemoveStumps.Value;
        stump.m_ttl = remove ? RemovedStumpSeconds : originalLifetime[stump];
    }

    private static GameObject FirstKnown(IEnumerable<string> names)
    {
        return names.Select(n => ZNetScene.instance.GetPrefab(n)).FirstOrDefault(p => p != null);
    }

    private static GameObject AnySapling()
    {
        List<GameObject> all = saplingFor.Values.SelectMany(n => n).Distinct()
            .Select(n => ZNetScene.instance.GetPrefab(n)).Where(p => p != null).ToList();
        return all.Count > 0 ? all[Random.Range(0, all.Count)] : null;
    }

    private static IEnumerator Plant(GameObject sapling, Vector3 where)
    {
        yield return new WaitForSeconds(MainConfig.replantDelay.Value);
        if (sapling != null)
        {
            Object.Instantiate(sapling, where, Quaternion.identity);
        }
    }
}
