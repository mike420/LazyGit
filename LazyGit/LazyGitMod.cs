using System.Reflection;
using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine;

namespace LazyGit;

// Lazy Git: the base looks after itself. Fires and torches take their fuel from nearby chests.
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.HardDependency)]
[NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
public class LazyGitMod : BaseUnityPlugin
{
    public const string PluginName = "LazyGit";
    public const string PluginVersion = "1.0.0";
    public const string PluginAuthor = "MagicMike";
    internal const string PluginGUID = "MagicMike.LazyGit";
    internal const string PluginCopy = "Copyright © MagicMike 2026";
    public const string PluginLink = "https://discord.gg/mnr36G6J4x";

    // How often the chores are done, in seconds. A fire only needs looking at every few seconds, and nothing is
    // searched for unless a fire is actually running low.
    private const float ChoreInterval = 5f;

    private Harmony harmony;
    private float nextChores;

    public void Awake()
    {
        MainConfig.Init(Config);
        MagicMike.ModSettings.ModSettings.Init(this);

        if (!MainConfig.modEnabled.Value)
        {
            return;
        }

        Translations.Load(this);
        harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGUID);

        // The mod can be loaded while a world is already running: pick up what is there
        ChestIndex.TrackExisting();
        FuelKeeper.TrackExisting();
        StationKeeper.TrackExisting();
        CookingKeeper.TrackExisting();
        FermentKeeper.TrackExisting();
        BallistaKeeper.TrackExisting();
        DoorKeeper.TrackExisting();
        RepairKeeper.TrackExisting();
        CollectKeeper.TrackExisting();
    }

    public void Update()
    {
        if (!MainConfig.modEnabled.Value || Player.m_localPlayer == null || ZNet.instance == null || Time.unscaledTime < nextChores)
        {
            return;
        }
        nextChores = Time.unscaledTime + ChoreInterval;

        ChestIndex.NewRound();
        if (MainConfig.fuelEnabled.Value)
        {
            FuelKeeper.DoRound();
        }
        if (MainConfig.stationEnabled.Value)
        {
            StationKeeper.DoRound();
        }
        if (MainConfig.cookingEnabled.Value)
        {
            CookingKeeper.DoRound();
        }
        if (MainConfig.fermentEnabled.Value)
        {
            FermentKeeper.DoRound();
        }
        if (MainConfig.ballistaEnabled.Value)
        {
            BallistaKeeper.DoRound();
        }
        if (MainConfig.doorEnabled.Value)
        {
            DoorKeeper.DoRound();
        }
        if (MainConfig.repairEnabled.Value)
        {
            RepairKeeper.DoRound();
        }
        if (MainConfig.collectEnabled.Value)
        {
            CollectKeeper.DoRound();
        }
    }

    public void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}
