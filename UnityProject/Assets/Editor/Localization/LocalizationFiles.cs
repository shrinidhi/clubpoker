using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClubPoker.Core;
using Newtonsoft.Json;
using UnityEditor;

/// <summary>
/// Shared file handling for the localization editor tools.
///
///   Assets/Resources/Localization/{code}.json   — the game's text, key → text
///   Localization/translation_memory.json        — English → translations, from
///       an imported sheet that had no keys yet (the scan CSV). Tagging a text
///       later fills its translations from here automatically.
///
/// JSON is written with keys sorted, so git diffs and merges stay small.
/// </summary>
internal static class LocalizationFiles
{
    public const string JsonDir = "Assets/Resources/Localization";
    public const string MemoryPath = "Localization/translation_memory.json";

    public static IEnumerable<string> Codes => Loc.Languages.Select(l => l.Code);
    public static IEnumerable<string> TranslatedCodes => Codes.Where(c => c != Loc.DefaultLanguage);

    // ── Language files ──────────────────────────────────────────────────────

    public static SortedDictionary<string, string> Load(string code)
    {
        string path = $"{JsonDir}/{code}.json";
        if (!File.Exists(path))
            return new SortedDictionary<string, string>(StringComparer.Ordinal);

        var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path))
                   ?? new Dictionary<string, string>();
        return new SortedDictionary<string, string>(data, StringComparer.Ordinal);
    }

    public static void Save(string code, SortedDictionary<string, string> table)
    {
        Directory.CreateDirectory(JsonDir);
        string path = $"{JsonDir}/{code}.json";
        File.WriteAllText(path, JsonConvert.SerializeObject(table, Formatting.Indented) + "\n",
                          new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path);
    }

    // ── Translation memory (English → language → text) ──────────────────────

    public static Dictionary<string, Dictionary<string, string>> LoadMemory()
    {
        if (!File.Exists(MemoryPath))
            return new Dictionary<string, Dictionary<string, string>>();

        return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(MemoryPath))
               ?? new Dictionary<string, Dictionary<string, string>>();
    }

    public static void SaveMemory(Dictionary<string, Dictionary<string, string>> memory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MemoryPath));
        var sorted = new SortedDictionary<string, Dictionary<string, string>>(memory, StringComparer.Ordinal);
        File.WriteAllText(MemoryPath, JsonConvert.SerializeObject(sorted, Formatting.Indented) + "\n",
                          new UTF8Encoding(false));
    }

    /// <summary>Lookup form of English text: case, spacing and zero-width spaces ignored.</summary>
    public static string MatchKey(string english) =>
        LocalizationScanTool.Normalize(english).ToLowerInvariant();

    // ── CSV ─────────────────────────────────────────────────────────────────

    public static string CsvField(string s)
    {
        s = s ?? string.Empty;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// RFC 4180 reader: quoted fields, "" escapes, newlines inside quotes.
    /// Comma or semicolon (Excel in some locales saves with ';'), detected
    /// from the header line.
    /// </summary>
    public static List<List<string>> ReadCsv(string path)
    {
        string text = File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿');
        string firstLine = text.Split('\n')[0];
        char sep = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';

        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            if (c == '"') inQuotes = true;
            else if (c == sep) { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n')
            {
                row.Add(field.ToString()); field.Clear();
                rows.Add(row); row = new List<string>();
            }
            else if (c != '\r') field.Append(c);
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows.Where(r => r.Any(f => !string.IsNullOrWhiteSpace(f))).ToList();
    }

    /// <summary>
    /// Column header → language code. Accepts the code ("es"), the native name
    /// ("Español") or the English name ("Spanish"), any case.
    /// </summary>
    public static string LanguageForHeader(string header)
    {
        string h = (header ?? "").Trim().ToLowerInvariant();
        foreach (var (code, native) in Loc.Languages)
            if (h == code || h == native.ToLowerInvariant())
                return code;

        switch (h)
        {
            case "english":    return "en";
            case "spanish":    return "es";
            case "portuguese": return "pt";
            case "russian":    return "ru";
            case "italian":    return "it";
            case "german":     return "de";
            default:           return null;
        }
    }
}
