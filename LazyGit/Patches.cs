using HarmonyLib;

namespace LazyGit;

// The mod keeps its own lists of the fires and chests in the world, so a round never has to search the whole scene.
// The game announces each one as it wakes up.
[HarmonyPatch(typeof(Fireplace), "Awake")]
internal static class FireplaceAwakePatch
{
    private static void Postfix(Fireplace __instance)
    {
        FuelKeeper.Track(__instance);
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
        StationKeeper.Track(__instance);
    }
}

[HarmonyPatch(typeof(CookingStation), "Awake")]
internal static class CookingStationAwakePatch
{
    private static void Postfix(CookingStation __instance)
    {
        CookingKeeper.Track(__instance);
    }
}

[HarmonyPatch(typeof(Fermenter), "Awake")]
internal static class FermenterAwakePatch
{
    private static void Postfix(Fermenter __instance)
    {
        FermentKeeper.Track(__instance);
    }
}

[HarmonyPatch(typeof(Turret), "Awake")]
internal static class TurretAwakePatch
{
    private static void Postfix(Turret __instance)
    {
        BallistaKeeper.Track(__instance);
    }
}

// Every kind of fire and station gets its own on/off setting, listed once the game knows all its pieces
[HarmonyPatch(typeof(ZNetScene), "Awake")]
internal static class ZNetSceneAwakePatch
{
    private static void Postfix(ZNetScene __instance)
    {
        FuelKeeper.ListFireKinds(__instance);
        StationKeeper.ListStationKinds(__instance);
        CookingKeeper.ListKinds(__instance);
        FermentKeeper.ListKinds(__instance);
        BallistaKeeper.ListKinds(__instance);
    }
}
