using System;
using System.Collections.Generic;

namespace LazyGit;

// Everything the mod runs from inside the game's own methods (patches) or from every frame goes through here: if it
// fails, the failure is logged (the first few times) and the game carries on. A mistake in this mod must never stop
// the game's own code from running, or fill the log with the same error every frame.
internal static class Safe
{
    private const int LoggedPerPlace = 3;

    private static readonly Dictionary<string, int> failures = new Dictionary<string, int>();

    public static void Run(string place, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            failures.TryGetValue(place, out int before);
            failures[place] = before + 1;
            if (before < LoggedPerPlace)
            {
                Jotunn.Logger.LogError($"[{LazyGitMod.PluginName}] {place} failed: {e}");
            }
        }
    }
}
