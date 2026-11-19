namespace LazyGit;

// Auto Repair Gear: opening a crafting station repairs the equipment in your inventory that the station can repair.
// Only the game's own repair is used, so its rules and costs are unchanged (a workbench still cannot repair forge gear).
internal static class GearRepair
{
    public static void OnStationUsed(CraftingStation station, Humanoid user, bool repeat)
    {
        if (!MainConfig.modEnabled.Value || !MainConfig.gearRepairEnabled.Value || repeat)
        {
            return;
        }

        // Only when the local player has really opened this station (not a held click, not a refused one)
        Player me = Player.m_localPlayer;
        if (me == null || user != me || me.GetCurrentCraftingStation() != station || InventoryGui.instance == null)
        {
            return;
        }

        // One item is repaired per call; at most once for every item carried, so it can never loop for ever
        int most = me.GetInventory().GetAllItems().Count;
        for (int done = 0; done < most && InventoryGui.instance.HaveRepairableItems(); done++)
        {
            InventoryGui.instance.RepairOneItem();
        }
    }
}
