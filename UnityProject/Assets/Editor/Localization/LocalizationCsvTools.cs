using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Translator round-trip.
///
/// Export: every key in en.json → Localization/translations.csv
///         (key | en | es | pt | ru | it | de), existing translations filled in,
///         so a re-export only leaves the new texts empty.
///
/// Import: reads a sheet back. Two formats:
///   - Has a "key" column (an Export)      → matched by key.
///   - No key column (the scan CSV, keys    → matched by English text, and
///     not final yet)                         remembered for texts not tagged
///                                            yet (translation_memory.json).
///   Language columns by code, native or English name ("ru", "Русский", "Russian").
///   Empty cells never overwrite; English is never changed by an import.
/// </summary>
internal static class LocalizationCsvTools
{
    private const string ExportPath = "Localization/translations.csv";

    [MenuItem("Tools/ClubPoker/Localization/Export CSV for Translator")]
    public static void Export()
    {
        var english = LocalizationFiles.Load("en");
        if (english.Count == 0)
        {
            EditorUtility.DisplayDialog("Export", "en.json has no keys yet — tag some texts first.", "OK");
            return;
        }

        string[] codes = LocalizationFiles.Codes.ToArray();
        var tables = codes.ToDictionary(c => c, LocalizationFiles.Load);

        var sb = new StringBuilder("key," + string.Join(",", codes) + "\n");
        int missing = 0;
        foreach (string key in english.Keys)
        {
            sb.Append(LocalizationFiles.CsvField(key));
            foreach (string code in codes)
            {
                tables[code].TryGetValue(key, out string value);
                if (code != "en" && string.IsNullOrEmpty(value))
                    missing++;
                sb.Append(',').Append(LocalizationFiles.CsvField(value));
            }
            sb.Append('\n');
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ExportPath));
        File.WriteAllText(ExportPath, sb.ToString(), new UTF8Encoding(true));   // BOM: Excel reads UTF-8

        string msg = $"{english.Count} keys exported, {missing} translations still empty.\n\n{Path.GetFullPath(ExportPath)}";
        Debug.Log("[Localization] " + msg.Replace("\n", " "));
        if (EditorUtility.DisplayDialog("Export done", msg, "Open folder", "Close"))
            EditorUtility.RevealInFinder(ExportPath);
    }

    [MenuItem("Tools/ClubPoker/Localization/Import CSV from Translator...")]
    public static void Import()
    {
        string path = EditorUtility.OpenFilePanel("Import translations CSV", "Localization", "csv");
        if (string.IsNullOrEmpty(path))
            return;

        List<List<string>> rows = LocalizationFiles.ReadCsv(path);
        if (rows.Count < 2)
        {
            EditorUtility.DisplayDialog("Import", "The file has no rows.", "OK");
            return;
        }

        List<string> header = rows[0];
        int keyCol = header.FindIndex(h => h.Trim().Equals("key", StringComparison.OrdinalIgnoreCase));
        var langCols = new Dictionary<int, string>();
        for (int i = 0; i < header.Count; i++)
        {
            string code = LocalizationFiles.LanguageForHeader(header[i]);
            if (code != null && !langCols.ContainsValue(code))
                langCols[i] = code;
        }

        int enCol = langCols.FirstOrDefault(kv => kv.Value == "en").Key;
        bool hasEnglish = langCols.ContainsValue("en");
        var targetCols = langCols.Where(kv => kv.Value != "en").ToList();

        if (targetCols.Count == 0 || (keyCol < 0 && !hasEnglish))
        {
            EditorUtility.DisplayDialog("Import",
                "Couldn't find the columns. Need a \"key\" or an English column, plus at least one of: " +
                string.Join(", ", LocalizationFiles.TranslatedCodes) + ".", "OK");
            return;
        }

        var english = LocalizationFiles.Load("en");
        var tables = LocalizationFiles.TranslatedCodes.ToDictionary(c => c, LocalizationFiles.Load);

        int applied = 0, unknownKeys = 0, remembered = 0;

        if (keyCol >= 0)
        {
            foreach (var row in rows.Skip(1))
            {
                string key = Cell(row, keyCol).Trim();
                if (key.Length == 0) continue;
                if (!english.ContainsKey(key)) { unknownKeys++; continue; }

                foreach (var (col, code) in targetCols.Select(kv => (kv.Key, kv.Value)))
                {
                    string value = Cell(row, col);
                    if (value.Trim().Length == 0) continue;
                    tables[code][key] = value;
                    applied++;
                }
            }
        }
        else
        {
            // By English text: fill every key with that English now, and keep
            // the rest for texts tagged later.
            var keysByEnglish = english
                .GroupBy(kv => LocalizationFiles.MatchKey(kv.Value))
                .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList());
            var memory = LocalizationFiles.LoadMemory();

            foreach (var row in rows.Skip(1))
            {
                string match = LocalizationFiles.MatchKey(Cell(row, enCol));
                if (match.Length == 0) continue;

                keysByEnglish.TryGetValue(match, out List<string> keys);
                bool any = false;

                foreach (var (col, code) in targetCols.Select(kv => (kv.Key, kv.Value)))
                {
                    string value = Cell(row, col);
                    if (value.Trim().Length == 0) continue;

                    if (!memory.TryGetValue(match, out var langs))
                        memory[match] = langs = new Dictionary<string, string>();
                    langs[code] = value;
                    any = true;

                    if (keys != null)
                        foreach (string key in keys) { tables[code][key] = value; applied++; }
                }

                if (any && keys == null) remembered++;
            }

            LocalizationFiles.SaveMemory(memory);
        }

        foreach (var kv in tables)
            LocalizationFiles.Save(kv.Key, kv.Value);

        string msg = $"{applied} translations written to the language files.";
        if (remembered > 0) msg += $"\n{remembered} texts aren't tagged yet — saved, and filled in automatically when tagged.";
        if (unknownKeys > 0) msg += $"\n{unknownKeys} rows had keys that no longer exist — skipped.";

        Debug.Log("[Localization] Import: " + msg.Replace("\n", " "));
        EditorUtility.DisplayDialog("Import done", msg, "OK");
    }

    private static string Cell(List<string> row, int col) =>
        col >= 0 && col < row.Count ? row[col] ?? string.Empty : string.Empty;
}
