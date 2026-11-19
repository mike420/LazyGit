using HarmonyLib;

namespace LazyGit;

// The mod keeps its own lists of the fires and chests in the world, so a round never has to search the whole scene.
// The game announces each one as it wakes up.
[HarmonyPatch(typeof(Fireplace), "Awake")]
internal static class FireplaceAwakePatch
{
    private static void Postfix(Fireplace __instance)
    {
        Safe.Run("FuelKeeper.Track", () => FuelKeeper.Track(__instance));
    }
}

[HarmonyPatch(typeof(Container), "Awake")]
internal static class ContainerAwakePatch
{
    private static void Postfix(Container __instance)
    {
        ChestIndex.Track(__instance);
    }
}

[HarmonyPatch(typeof(Smelter), "Awake")]
internal static class SmelterAwakePatch
{
    private static void Postfix(Smelter __instance)
    {
        Safe.Run("StationKeeper.Track", () => StationKeeper.Track(__instance));
    }
}

[HarmonyPatch(typeof(CookingStation), "Awake")]
internal static class CookingStationAwakePatch
{
    private static void Postfix(CookingStation __instance)
    {
        Safe.Run("CookingKeeper.Track", () => CookingKeeper.Track(__instance));
    }
}

[HarmonyPatch(typeof(Fermenter), "Awake")]
internal static class FermenterAwakePatch
{
    private static void Postfix(Fermenter __instance)
    {
        Safe.Run("FermentKeeper.Track", () => FermentKeeper.Track(__instance));
    }
}

[HarmonyPatch(typeof(Turret), "Awake")]
internal static class TurretAwakePatch
{
    private static void Postfix(Turret __instance)
    {
        Safe.Run("BallistaKeeper.Track", () => BallistaKeeper.Track(__instance));
    }
}

[HarmonyPatch(typeof(ShieldGenerator), "Start")]
internal static class ShieldGeneratorStartPatch
{
    private static void Postfix(ShieldGenerator __instance)
    {
        Safe.Run("ShieldGeneratorKeeper.Track", () => ShieldGeneratorKeeper.Track(__instance));
    }
}

[HarmonyPatch(typeof(Door), "Awake")]
internal static class DoorAwakePatch
{
    private static void Postfix(Door __instance)
    {
        Safe.Run("DoorKeeper.Track", () => DoorKeeper.Track(__instance));
    }
}

[HarmonyPatch(typeof(WearNTear), "Awake")]
internal static class WearNTearAwakePatch
{
    private static void Postfix(WearNTear __instance)
    {
        Safe.Run("RepairKeeper.Track", () => RepairKeeper.Track(__instance));
    }
}

[HarmonyPatch(typeof(Beehive), "Awake")]
internal static class BeehiveAwakePatch
{
    private static void Postfix(Beehive __instance)
    {
        Safe.Run("CollectKeeper.Track", () => CollectKeeper.Track(__instance));
    }
}

[HarmonyPatch(typeof(SapCollector), "Awake")]
internal static class SapCollectorAwakePatch
{
    private static void Postfix(SapCollector __instance)
    {
        Safe.Run("CollectKeeper.Track", () => CollectKeeper.Track(__instance));
    }
}

// Auto Repair Gear: when a crafting station is opened
[HarmonyPatch(typeof(CraftingStation), "Interact")]
internal static class CraftingStationInteractPatch
{
    private static void Postfix(CraftingStation __instance, Humanoid user, bool repeat)
    {
        Safe.Run("GearRepair.OnStationUsed", () => GearRepair.OnStationUsed(__instance, user, repeat));
    }
}

// Auto Shield: what the player's hands hold
[HarmonyPatch(typeof(Humanoid), "EquipItem")]
internal static class HumanoidEquipItemPatch
{
    private static void Postfix(Humanoid __instance)
    {
        Safe.Run("ShieldKeeper.OnEquipmentChanged", () => ShieldKeeper.OnEquipmentChanged(__instance));
    }
}

[HarmonyPatch(typeof(Humanoid), "UnequipItem")]
internal static class HumanoidUnequipItemPatch
{
    private static void Prefix(Humanoid __instance, ItemDrop.ItemData item)
    {
        Safe.Run("ShieldKeeper.BeforeUnequip", () => ShieldKeeper.BeforeUnequip(__instance, item));
    }

    private static void Postfix(Humanoid __instance)
    {
        Safe.Run("ShieldKeeper.OnEquipmentChanged", () => ShieldKeeper.OnEquipmentChanged(__instance));
    }
}

[HarmonyPatch(typeof(Humanoid), "HideHandItems")]
internal static class HumanoidHideHandItemsPatch
{
    private static void Postfix(Humanoid __instance, bool onlyRightHand)
    {
        Safe.Run("ShieldKeeper.OnHandsHidden", () => ShieldKeeper.OnHandsHidden(__instance, onlyRightHand));
    }
}

[HarmonyPatch(typeof(Humanoid), "ShowHandItems")]
internal static class HumanoidShowHandItemsPatch
{
    private static void Prefix(Humanoid __instance, bool onlyRightHand)
    {
        Safe.Run("ShieldKeeper.BeforeHandsShown", () => ShieldKeeper.BeforeHandsShown(__instance, onlyRightHand));
    }

    private static void Postfix(Humanoid __instance, bool onlyRightHand)
    {
        Safe.Run("ShieldKeeper.OnHandsShown", () => ShieldKeeper.OnHandsShown(__instance, onlyRightHand));
    }
}

[HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
internal static class AttackTriggerPatch
{
    private static void Prefix(Attack __instance)
    {
        Safe.Run("ShieldKeeper.OnAttackTrigger", () => ShieldKeeper.OnAttackTrigger(__instance));
    }
}

// Auto Replant: a stump (or a tree with no stump) is destroyed; a felled tree is about to leave its stump; saplings
// may grow without room around them
[HarmonyPatch(typeof(Destructible), "Destroy")]
internal static class DestructibleDestroyPatch
{
    private static void Prefix(Destructible __instance)
    {
        Safe.Run("TreeKeeper.OnDestroyed", () => TreeKeeper.OnDestroyed(__instance));
    }
}

[HarmonyPatch(typeof(TreeBase), "SpawnLog")]
internal static class TreeBaseSpawnLogPatch
{
    private static void Prefix(TreeBase __instance)
    {
        Safe.Run("TreeKeeper.SetStumpLifetime", () => TreeKeeper.SetStumpLifetime(__instance));
    }
}

[HarmonyPatch(typeof(Plant), "HaveGrowSpace")]
internal static class PlantGrowSpacePatch
{
    private static void Postfix(ref bool __result)
    {
        if (MainConfig.modEnabled.Value && MainConfig.replantEnabled.Value && MainConfig.replantIgnoreSpace.Value)
        {
            __result = true;
        }
    }
}

// Every kind of fire and station gets its own on/off setting, listed once the game knows all its pieces
[HarmonyPatch(typeof(ZNetScene), "Awake")]
internal static class ZNetSceneAwakePatch
{
    // Before the game's pieces are joined by those of the mods
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ZNetScene __instance)
    {
        Safe.Run("MainConfig.RememberVanilla", () => MainConfig.RememberVanilla(__instance));
    }

    private static void Postfix(ZNetScene __instance)
    {
        MainConfig.Batch(() =>
        {
            Safe.Run("FuelKeeper.ListFireKinds", () => FuelKeeper.ListFireKinds(__instance));
            Safe.Run("StationKeeper.ListStationKinds", () => StationKeeper.ListStationKinds(__instance));
            Safe.Run("CookingKeeper.ListKinds", () => CookingKeeper.ListKinds(__instance));
            Safe.Run("FermentKeeper.ListKinds", () => FermentKeeper.ListKinds(__instance));
            Safe.Run("BallistaKeeper.ListKinds", () => BallistaKeeper.ListKinds(__instance));
            Safe.Run("ShieldGeneratorKeeper.ListKinds", () => ShieldGeneratorKeeper.ListKinds(__instance));
            Safe.Run("DoorKeeper.ListKinds", () => DoorKeeper.ListKinds(__instance));
        });
    }
}
