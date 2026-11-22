using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LazyGit;

// Auto Close Doors: a door or gate you built that has been left open, with nobody near it, is closed.
// Auto Open Doors: a door or gate you built opens when you walk up to it (see OpenUpdate).
internal static class DoorKeeper
{
    private const string KindSection = "Door Types";

    private static readonly HashSet<Door> doors = new HashSet<Door>();

    // The on/off setting of each kind of door, by its prefab name
    private static readonly Dictionary<string, ConfigEntry<bool>> kinds = new Dictionary<string, ConfigEntry<bool>>();

    // When somebody was last near each open door (or when the door was first seen open with nobody there)
    private static readonly Dictionary<Door, float> lastBusy = new Dictionary<Door, float>();

    // Auto Open: doors seen open while the local player was near them, doors the player closed by hand (left closed
    // until the player has gone away), and when each door was last asked to open
    private static readonly HashSet<Door> seenOpenNear = new HashSet<Door>();
    private static readonly HashSet<Door> closedByHand = new HashSet<Door>();
    private static readonly Dictionary<Door, float> askedToOpen = new Dictionary<Door, float>();
    private static float nextOpenLook;

    private const float OpenLookInterval = 0.2f;
    private const float OpenRetrySeconds = 1f;
    private const float LeaveMargin = 1.5f;

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
        foreach (GameObject prefab in MainConfig.VanillaFirst(scene.m_prefabs))
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
        foreach (Door gone in pairs.Keys.Where(d => d == null).ToList())
        {
            pairs.Remove(gone);
        }
        seenOpenNear.RemoveWhere(d => d == null);
        closedByHand.RemoveWhere(d => d == null);
        foreach (Door gone in askedToOpen.Keys.Where(d => d == null).ToList())
        {
            askedToOpen.Remove(gone);
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
        // (A double door stays open while somebody is near either half)
        List<Door> partners = PartnersOf(door, Time.time);
        bool somebodyNear = players.Any(p => p != null && ((p.transform.position - door.transform.position).sqrMagnitude <= maxSqr
            || partners.Any(other => other != null && (p.transform.position - other.transform.position).sqrMagnitude <= maxSqr)));
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

    // Called every frame by the plugin; looks at the doors near the local player a few times a second
    public static void OpenUpdate()
    {
        Player player = Player.m_localPlayer;
        if (!MainConfig.doorOpenEnabled.Value || player == null || player.IsDead() || player.IsTeleporting()
            || Time.timeScale == 0f || Time.unscaledTime < nextOpenLook)
        {
            return;
        }
        nextOpenLook = Time.unscaledTime + OpenLookInterval;

        // (Never wider than the distance that keeps a door open, or a door would be closed again as it opened)
        float reach = Mathf.Min(MainConfig.doorOpenDistance.Value, MainConfig.doorPlayerDistance.Value);
        // (A door's pivot can be some way from the part nearest the player, and a partner is some way from the door)
        float coarse = reach + LeaveMargin + 8f + (MainConfig.doorPairEnabled.Value ? MainConfig.doorPairDistance.Value : 0f);
        float coarseSqr = coarse * coarse;
        Vector3 here = player.transform.position;
        float now = Time.time;

        foreach (Door door in doors)
        {
            if (door == null || (door.transform.position - here).sqrMagnitude > coarseSqr)
            {
                continue;
            }
            LookToOpen(door, player, here, reach, now);
        }
    }

    private static void LookToOpen(Door door, Player player, Vector3 here, float reach, float now)
    {
        ZNetView view = viewOf(door);
        Piece piece = door.GetComponent<Piece>();
        if (view == null || !view.IsValid() || piece == null || !piece.IsPlacedByPlayer() || !CanBeClosed(door)
            || !KindSetting(Utils.GetPrefabName(door.gameObject), piece).Value)
        {
            return;
        }

        // A double door is one door: near either half is near both, so both open together
        float distance = DistanceTo(door, here);
        foreach (Door other in PartnersOf(door, now))
        {
            if (other != null)
            {
                distance = Mathf.Min(distance, DistanceTo(other, here));
            }
        }
        if (distance > reach + LeaveMargin)
        {
            seenOpenNear.Remove(door);
            closedByHand.Remove(door);
            return;
        }
        if (distance > reach)
        {
            return;
        }

        if (view.GetZDO().GetInt(ZDOVars.s_state, 0) != 0)
        {
            seenOpenNear.Add(door);
            return;
        }

        // Closed. If it was open while the player was here, the player closed it: leave it be until they go away
        if (seenOpenNear.Remove(door))
        {
            closedByHand.Add(door);
        }
        if (closedByHand.Contains(door))
        {
            return;
        }
        if (askedToOpen.TryGetValue(door, out float asked) && now - asked < OpenRetrySeconds)
        {
            return;
        }
        if (door.m_checkGuardStone && !PrivateArea.CheckAccess(door.transform.position, 0f, false))
        {
            return;
        }

        // The same call a player's click makes: the door opens away from the player
        Vector3 fromDoor = (player.transform.position - door.transform.position).normalized;
        view.InvokeRPC("UseDoor", Vector3.Dot(door.transform.forward, fromDoor) < 0f);
        askedToOpen[door] = now;
    }

    private class PairInfo
    {
        public List<Door> Partners = new List<Door>();
        public float ScannedAt = float.MinValue;
    }

    private static readonly Dictionary<Door, PairInfo> pairs = new Dictionary<Door, PairInfo>();
    private static readonly List<Door> noPartners = new List<Door>();

    private const float PairScanSeconds = 10f;
    private const float PairMaxHeightDifference = 1f;

    private static bool IsOpen(Door door)
    {
        ZNetView view = viewOf(door);
        return view != null && view.IsValid() && view.GetZDO().GetInt(ZDOVars.s_state, 0) != 0;
    }

    // The doors this one forms a double door with: the same kind of door, side by side. The shape of a door changes as
    // it swings, so the doors are only measured while both are closed; one that is open keeps the partners it had.
    private static List<Door> PartnersOf(Door door, float now)
    {
        if (!MainConfig.doorPairEnabled.Value)
        {
            return noPartners;
        }
        if (!pairs.TryGetValue(door, out PairInfo info))
        {
            info = new PairInfo();
            pairs[door] = info;
        }
        if (now - info.ScannedAt < PairScanSeconds || IsOpen(door))
        {
            return info.Partners;
        }
        info.ScannedAt = now;

        List<Door> found = info.Partners.Where(other => other != null && IsOpen(other)).ToList();
        Bounds mine = BoundsOf(door);
        string kind = Utils.GetPrefabName(door.gameObject);
        float maxGap = MainConfig.doorPairDistance.Value;
        foreach (Door other in doors)
        {
            if (other == null || other == door || found.Contains(other) || IsOpen(other) || Utils.GetPrefabName(other.gameObject) != kind)
            {
                continue;
            }
            Bounds theirs = BoundsOf(other);
            if (Mathf.Abs(mine.center.y - theirs.center.y) < PairMaxHeightDifference && GapBetween(mine, theirs) <= maxGap)
            {
                found.Add(other);
            }
        }
        info.Partners = found;
        return found;
    }

    private static Bounds BoundsOf(Door door)
    {
        Bounds bounds = new Bounds(door.transform.position, Vector3.zero);
        foreach (Collider collider in door.GetComponentsInChildren<Collider>())
        {
            if (collider != null && collider.enabled)
            {
                bounds.Encapsulate(collider.bounds);
            }
        }
        return bounds;
    }

    // The distance between the nearest faces of two boxes (0 if they touch)
    private static float GapBetween(Bounds a, Bounds b)
    {
        float x = Mathf.Max(0f, a.min.x - b.max.x, b.min.x - a.max.x);
        float y = Mathf.Max(0f, a.min.y - b.max.y, b.min.y - a.max.y);
        float z = Mathf.Max(0f, a.min.z - b.max.z, b.min.z - a.max.z);
        return Mathf.Sqrt(x * x + y * y + z * z);
    }

    // How far the point is from the nearest part of the door (its pivot is at the hinge, which is far from a wide gate)
    private static float DistanceTo(Door door, Vector3 point)
    {
        float best = float.MaxValue;
        foreach (Collider collider in door.GetComponentsInChildren<Collider>())
        {
            if (collider != null && collider.enabled)
            {
                best = Mathf.Min(best, Vector3.Distance(collider.bounds.ClosestPoint(point), point));
            }
        }
        return best < float.MaxValue ? best : Vector3.Distance(door.transform.position, point);
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

        string pieceName = EnglishText.Of(piece.m_name, prefabName);
        setting = MainConfig.BindKind(KindSection, prefabName, pieceName, $"{pieceName} is opened when you walk up to it, and closed when it has been left open with nobody near it.");
        kinds[prefabName] = setting;
        return setting;
    }
}
