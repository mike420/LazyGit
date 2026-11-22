using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Light Fires: the fires you built that can be switched on and off (the game's own on/off of a brazier or a
// torch) are lit as it gets dark, and put out when the day comes, which saves their fuel. A fire is switched once at
// each change from day to night and back, so one you switch by hand in between stays as you left it.
internal static class LightKeeper
{
    private const string KindSection = "Light Types";

    private static readonly HashSet<Fireplace> fires = new HashSet<Fireplace>();

    // The on/off setting of each kind of fire, by its prefab name
    private static readonly Dictionary<string, ConfigEntry<bool>> kinds = new Dictionary<string, ConfigEntry<bool>>();

    // The last change of the light this fire was dealt with for, kept in the fire's own data: 1 for night, 2 for day
    private const string DoneKey = "LazyGit_light";
    private const int Night = 1;
    private const int Day = 2;

    private static readonly AccessTools.FieldRef<Fireplace, ZNetView> viewOf = AccessTools.FieldRefAccess<Fireplace, ZNetView>("m_nview");
    private static readonly AccessTools.FieldRef<Fireplace, bool> wetOf = AccessTools.FieldRefAccess<Fireplace, bool>("m_wet");

    // The game's own values of the state of a fire: 1 is on, 2 is off (a new fire has none stored, which is on)
    private const int On = 1;

    public static void Track(Fireplace fire)
    {
        fires.Add(fire);
    }

    public static void TrackExisting()
    {
        foreach (Fireplace fire in UnityEngine.Object.FindObjectsByType<Fireplace>(FindObjectsSortMode.None))
        {
            fires.Add(fire);
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
            Fireplace fire = prefab != null ? prefab.GetComponent<Fireplace>() : null;
            Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
            if (fire != null && piece != null && fire.m_canTurnOff)
            {
                KindSetting(prefab.name, piece);
            }
        }
    }

    public static void DoRound()
    {
        fires.RemoveWhere(fire => fire == null);

        // Dusk and night are dark: the fires are wanted lit. Only full daylight puts them out.
        if (EnvMan.instance == null)
        {
            return;
        }
        bool daylight = EnvMan.IsDaylight();

        foreach (Fireplace fire in fires.ToList())
        {
            try
            {
                Switch(fire, !daylight);
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not switch the fire at {fire.transform.position}: {e.Message}");
            }
        }
    }

    private static void Switch(Fireplace fire, bool dark)
    {
        ZNetView view = viewOf(fire);
        if (!fire.m_canTurnOff || view == null || !view.IsValid())
        {
            return;
        }

        // Only the fire's owner looks after it (every player's game runs this, and the owner is whoever is near the
        // fire), so two players never switch it twice. It is not claimed for this: a fire is switched by whoever has it.
        if (!view.IsOwner())
        {
            return;
        }

        Piece piece = fire.GetComponent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer() || !KindSetting(Utils.GetPrefabName(fire.gameObject), piece).Value)
        {
            return;
        }

        // Once for each change of the light. It is written in the fire's own data, not kept in this game, so it holds
        // when the fire changes hands or the world is reloaded, and a fire switched by hand stays as it was left.
        int phase = dark ? Night : Day;
        ZDO zdo = view.GetZDO();
        if (zdo.GetInt(DoneKey, 0) == phase)
        {
            return;
        }

        bool burning = zdo.GetInt(ZDOVars.s_state, On) == On;
        if (dark)
        {
            // Nothing to light with no fuel, and the game puts a wet fire out itself: it would only be put out again
            if (!burning && (zdo.GetFloat(ZDOVars.s_fuel, 0f) <= 0f || wetOf(fire)))
            {
                return;
            }
            if (!burning)
            {
                view.InvokeRPC("RPC_ToggleOn");   // the same call a player's click makes: it switches from what it is
            }
        }
        else if (burning && MainConfig.lightPutOut.Value)
        {
            view.InvokeRPC("RPC_ToggleOn");
        }
        zdo.Set(DoneKey, phase);
    }

    private static ConfigEntry<bool> KindSetting(string prefabName, Piece piece)
    {
        if (kinds.TryGetValue(prefabName, out ConfigEntry<bool> setting))
        {
            return setting;
        }

        string pieceName = EnglishText.Of(piece.m_name, prefabName);
        setting = MainConfig.BindKind(KindSection, prefabName, pieceName, $"{pieceName} is lit when it gets dark, and put out in daylight.");
        kinds[prefabName] = setting;
        return setting;
    }
}
