using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LazyGit;

// In multiplayer the game hands each object to one player (its owner), and only the owner may change what it holds.
// A chore that needs to work on something another player owns asks for it here: the first time it is claimed and the
// work is done from the next round on. A claim is not repeated for a while, so two players both running the mod do
// not keep taking the same thing from each other.
internal static class Ownership
{
    private const float BackOffSeconds = 30f;

    private static readonly Dictionary<ZNetView, float> claimedAt = new Dictionary<ZNetView, float>();

    // True if the local player owns it and may go ahead now
    public static bool Hold(ZNetView view)
    {
        if (view.IsOwner())
        {
            return true;
        }

        float now = Time.unscaledTime;
        if (!claimedAt.TryGetValue(view, out float before) || now - before >= BackOffSeconds)
        {
            claimedAt[view] = now;
            view.ClaimOwnership();
        }
        return false;
    }

    public static void Prune()
    {
        foreach (ZNetView gone in claimedAt.Keys.Where(v => v == null).ToList())
        {
            claimedAt.Remove(gone);
        }
    }
}
