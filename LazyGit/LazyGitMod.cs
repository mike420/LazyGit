using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine;

namespace LazyGit;

// Lazy Git: the base looks after itself. Fires and torches take their fuel from nearby chests.
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.HardDependency)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Major)]
public class LazyGitMod : BaseUnityPlugin
{
    public const string PluginName = "LazyGit";
    public const string PluginVersion = "1.0.1";
    public const string PluginAuthor = "MagicMike";
    internal const string PluginGUID = "MagicMike.LazyGit";
    internal const string PluginCopy = "Copyright © MagicMike 2026";
    public const string PluginLink = "https://discord.gg/mnr36G6J4x";

    // How often the chores are done, in seconds. A fire only needs looking at every few seconds, and nothing is
    // searched for unless a fire is actually running low.
    private const float ChoreInterval = 5f;

    private Harmony harmony;
    private float nextChores;

    // So that code outside the plugin can start coroutines on it
    internal static LazyGitMod Instance { get; private set; }

    public void Awake()
    {
        Instance = this;
        MainConfig.Init(Config);
        MagicMike.ModSettings.ModSettings.Init(this);

        if (!MainConfig.modEnabled.Value)
        {
            return;
        }

        Translations.Load(this);
        // Each patch on its own: if the game has changed and one does not fit, that one is reported and left out, and
        // the rest of the mod still works
        harmony = new Harmony(PluginGUID);
        foreach (System.Type type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsDefined(typeof(HarmonyPatch), true)))
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (System.Exception e)
            {
                Logger.LogError($"Could not apply the patch {type.Name}: {e.Message}");
            }
        }

        // The mod can be loaded while a world is already running: pick up what is there
        MainConfig.Batch(() =>
        {
            Safe.Run("ChestIndex.TrackExisting", ChestIndex.TrackExisting);
            Safe.Run("FuelKeeper.TrackExisting", FuelKeeper.TrackExisting);
            Safe.Run("StationKeeper.TrackExisting", StationKeeper.TrackExisting);
            Safe.Run("CookingKeeper.TrackExisting", CookingKeeper.TrackExisting);
            Safe.Run("FermentKeeper.TrackExisting", FermentKeeper.TrackExisting);
            Safe.Run("BallistaKeeper.TrackExisting", BallistaKeeper.TrackExisting);
            Safe.Run("ShieldGeneratorKeeper.TrackExisting", ShieldGeneratorKeeper.TrackExisting);
            Safe.Run("DoorKeeper.TrackExisting", DoorKeeper.TrackExisting);
            Safe.Run("RepairKeeper.TrackExisting", RepairKeeper.TrackExisting);
            Safe.Run("CollectKeeper.TrackExisting", CollectKeeper.TrackExisting);
            Safe.Run("CropKeeper.TrackExisting", CropKeeper.TrackExisting);
            Safe.Run("LightKeeper.TrackExisting", LightKeeper.TrackExisting);
            Safe.Run("TidyKeeper.TrackExisting", TidyKeeper.TrackExisting);
        });
    }

    public void Update()
    {
        if (MainConfig.modEnabled.Value)
        {
            Safe.Run("ShieldKeeper.Update", ShieldKeeper.Update);
            Safe.Run("DoorKeeper.OpenUpdate", DoorKeeper.OpenUpdate);
        }

        if (!MainConfig.modEnabled.Value || Player.m_localPlayer == null || ZNet.instance == null || Time.timeScale == 0f || Time.unscaledTime < nextChores)
        {
            return;
        }
        nextChores = Time.unscaledTime + ChoreInterval;

        Safe.Run("Ownership.Prune", Ownership.Prune);
        Safe.Run("ChestIndex.NewRound", ChestIndex.NewRound);
        if (MainConfig.fuelEnabled.Value)
        {
            Safe.Run("FuelKeeper.DoRound", FuelKeeper.DoRound);
        }
        if (MainConfig.stationEnabled.Value)
        {
            Safe.Run("StationKeeper.DoRound", StationKeeper.DoRound);
        }
        if (MainConfig.cookingEnabled.Value)
        {
            Safe.Run("CookingKeeper.DoRound", CookingKeeper.DoRound);
        }
        if (MainConfig.fermentEnabled.Value)
        {
            Safe.Run("FermentKeeper.DoRound", FermentKeeper.DoRound);
        }
        if (MainConfig.ballistaEnabled.Value)
        {
            Safe.Run("BallistaKeeper.DoRound", BallistaKeeper.DoRound);
        }
        if (MainConfig.generatorEnabled.Value)
        {
            Safe.Run("ShieldGeneratorKeeper.DoRound", ShieldGeneratorKeeper.DoRound);
        }
        if (MainConfig.doorEnabled.Value)
        {
            Safe.Run("DoorKeeper.DoRound", DoorKeeper.DoRound);
        }
        if (MainConfig.repairEnabled.Value)
        {
            Safe.Run("RepairKeeper.DoRound", RepairKeeper.DoRound);
        }
        if (MainConfig.collectEnabled.Value)
        {
            Safe.Run("CollectKeeper.DoRound", CollectKeeper.DoRound);
        }
        if (MainConfig.cropEnabled.Value)
        {
            Safe.Run("CropKeeper.DoRound", CropKeeper.DoRound);
        }
        if (MainConfig.lightEnabled.Value)
        {
            Safe.Run("LightKeeper.DoRound", LightKeeper.DoRound);
        }
        if (MainConfig.tidyEnabled.Value)
        {
            Safe.Run("TidyKeeper.DoRound", TidyKeeper.DoRound);
        }
    }

    public void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}
