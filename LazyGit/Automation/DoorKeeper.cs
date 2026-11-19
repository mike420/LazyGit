using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Close Doors: a door or gate you built that has been left open, with nobody near it, is closed.
internal static class DoorKeeper
{
    private const string KindSection = "Door Types";

    private static readonly HashSet<Door> doors = new HashSet<Door>();

    // The on/off setting of each kind of door, by its prefab name
    private static readonly Dictionary<string, ConfigEntry<bool>> kinds = new Dictionary<string, ConfigEntry<bool>>();

    // When somebody was last near each open door (or when the door was first seen open with nobody there)
    private static readonly Dictionary<Door, float> lastBusy = new Dictionary<Door, float>();

    private static readonly AccessTools.FieldRef<Door, ZNetView> viewOf = AccessTools.FieldRefAccess<Door, ZNetView>("m_nview");

    public static void Track(Door door)
    {
        doors.Add(door);
    }

    public static void TrackExisting()
    {
        foreach (Door door in UnityEngine.Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
        {
            doors.Add(door);
        }
        if (ZNetScene.instance != null)
        {
            ListKinds(ZNetScene.instance);
        }
    }

    public static void ListKinds(ZNetScene scene)
    {
        foreach (GameObject prefab in scene.m_prefabs)
        {
            if (prefab == null)
            {
                continue;
            }
            Door door = prefab.GetComponent<Door>();
            Piece piece = prefab.GetComponent<Piece>();
            if (door != null && piece != null && CanBeClosed(door))
            {
                KindSetting(prefab.name, piece);
            }
        }
    }

    public static void DoRound()
    {
        doors.RemoveWhere(door => door == null);
        foreach (Door gone in lastBusy.Keys.Where(d => d == null).ToList())
        {
            lastBusy.Remove(gone);
        }

        // (Game time, which stands still while the game is paused: a pause is not time with nobody near)
        float now = Time.time;
        float keepOpenNear = MainConfig.doorPlayerDistance.Value;
        float delay = MainConfig.doorCloseAfter.Value;
        List<Player> players = Player.GetAllPlayers();

        foreach (Door door in doors.ToList())
        {
            try
            {
                Check(door, now, keepOpenNear, delay, players);
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not check the door at {door.transform.position}: {e.Message}");
            }
        }
    }

    private static void Check(Door door, float now, float keepOpenNear, float delay, List<Player> players)
    {
        ZNetView view = viewOf(door);
        if (view == null || !view.IsValid() || view.GetZDO().GetInt(ZDOVars.s_state, 0) == 0)
        {
            lastBusy.Remove(door);
            return;
        }

        Piece piece = door.GetComponent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer() || !CanBeClosed(door) || !KindSetting(Utils.GetPrefabName(door.gameObject), piece).Value)
        {
            lastBusy.Remove(door);
            return;
        }

        float maxSqr = keepOpenNear * keepOpenNear;
        bool somebodyNear = players.Any(p => p != null && (p.transform.position - door.transform.position).sqrMagnitude <= maxSqr);
        if (somebodyNear || !lastBusy.ContainsKey(door))
        {
            lastBusy[door] = now;
            return;
        }

        // Only the door's owner closes it: if two players both did, the second would open it again. A door that
        // another player owns is claimed now and closed from the next round on (the timer keeps running).
        if (now - lastBusy[door] >= delay && Ownership.Hold(view))
        {
            // The same call a player's click makes; on an open door it closes it
            view.InvokeRPC("UseDoor", true);
            lastBusy.Remove(door);
        }
    }

    // Doors that need a key, and doors made to stay open, cannot be closed by anyone
    private static bool CanBeClosed(Door door)
    {
        return door.m_keyItem == null && !door.m_canNotBeClosed;
    }

    private static ConfigEntry<bool> KindSetting(string prefabName, Piece piece)
    {
        if (kinds.TryGetValue(prefabName, out ConfigEntry<bool> setting))
        {
            return setting;
        }

        Localization text = Localization.instance;
        string pieceName = text != null ? text.Localize(piece.m_name) : prefabName;
        setting = MainConfig.File.Bind(KindSection, prefabName, true, $"{pieceName} is closed when it has been left open with nobody near it.");
        kinds[prefabName] = setting;
        return setting;
    }
}
