using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LazyGit;

// The English names of pieces and items, whatever language the game is set to. The settings file is always in English
// (and its switches must keep the same names if the player changes language), so what is written in it comes from here.
//
// The game keeps its text in CSV files with a column for each language; the English column is read here directly.
internal static class EnglishText
{
    private static Dictionary<string, string> words;

    // The English name of a piece or an item from its token ("$piece_workbench"); 'fallback' if it has none
    public static string Of(string token, string fallback)
    {
        if (string.IsNullOrEmpty(token))
        {
            return fallback;
        }

        words ??= Load();
        if (words.TryGetValue(token.TrimStart('$'), out string english) && english.Length > 0)
        {
            return english;
        }

        // Something another mod added: its name in the game's own language is the best there is
        string local = Localization.instance?.Localize(token);
        return string.IsNullOrWhiteSpace(local) || local.StartsWith("[") ? fallback : local;
    }

    private static Dictionary<string, string> Load()
    {
        Dictionary<string, string> found = new Dictionary<string, string>();
        try
        {
            LocalizationSettings settings = Resources.Load<LocalizationSettings>("LocalizationSettings");
            foreach (TextAsset file in settings.Localizations)
            {
                if (file != null)
                {
                    Read(file.text, found);
                }
            }
        }
        catch (System.Exception e)
        {
            Jotunn.Logger.LogWarning($"[{LazyGitMod.PluginName}] Could not read the English names of the pieces: {e.Message}");
        }
        return found;
    }

    // One file: the first line names the columns (key, English, ...); a later file replaces the words of an earlier one
    private static void Read(string text, Dictionary<string, string> into)
    {
        bool header = true;
        int english = 1;
        foreach (List<string> record in Records(text))
        {
            if (header)
            {
                header = false;
                int at = record.FindIndex(c => c.Trim() == "English");
                english = at >= 0 ? at : 1;
                continue;
            }

            // Only what is needed: the names of pieces and of items
            if (record.Count > english && (record[0].StartsWith("piece_") || record[0].StartsWith("item_")))
            {
                into[record[0]] = record[english].Trim();
            }
        }
    }

    // The lines of a CSV file as lists of fields: a field in quotes may hold commas and line breaks, and "" is a quote
    private static IEnumerable<List<string>> Records(string text)
    {
        List<string> fields = new List<string>();
        StringBuilder field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c != '"')
                {
                    field.Append(c);
                }
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = false;
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Length = 0;
            }
            else if (c == '\n')
            {
                fields.Add(field.ToString());
                field.Length = 0;
                yield return fields;
                fields = new List<string>();
            }
            else if (c != '\r')
            {
                field.Append(c);
            }
        }

        fields.Add(field.ToString());
        yield return fields;
    }
}
