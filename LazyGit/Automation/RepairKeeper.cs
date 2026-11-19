using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LazyGit;

// Auto Repair: the pieces you built that are worn or damaged, near you, are repaired the way the hammer does it.
internal static class RepairKeeper
{
    // So that a base badly knocked about is mended over a few rounds rather than all in one frame
    private const int MostPerRound = 30;

    private static readonly HashSet<WearNTear> pieces = new HashSet<WearNTear>();

    public static void Track(WearNTear piece)
    {
        pieces.Add(piece);
    }

    public static void TrackExisting()
    {
        foreach (WearNTear piece in UnityEngine.Object.FindObjectsByType<WearNTear>(FindObjectsSortMode.None))
        {
            pieces.Add(piece);
        }
    }

    public static void DoRound()
    {
        pieces.RemoveWhere(piece => piece == null);

        Vector3 here = Player.m_localPlayer.transform.position;
        float range = MainConfig.repairRange.Value;
        float maxSqr = range * range;
        float below = MainConfig.repairBelow.Value / 100f;
        int repaired = 0;

        foreach (WearNTear piece in pieces.ToList())
        {
            try
            {
                // The cheap checks first: most pieces are whole, or far away
                float health = piece.GetHealthPercentage();
                if (health >= below || health <= 0f || (piece.transform.position - here).sqrMagnitude > maxSqr)
                {
                    continue;
                }

                Piece built = piece.GetComponent<Piece>();
                if (built == null || !built.IsPlacedByPlayer() || !PrivateArea.CheckAccess(piece.transform.position, 0f, false))
                {
                    continue;
                }

                if (piece.Repair() && ++repaired >= MostPerRound)
                {
                    break;
                }
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not repair the piece at {piece.transform.position}: {e.Message}");
            }
        }
    }
}
