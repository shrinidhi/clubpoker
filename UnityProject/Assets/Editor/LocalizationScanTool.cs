using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Inventory of every player-visible text in the project — the starting point
/// for localization. Read-only: opens scenes additively to read them and closes
/// them without saving; nothing in Assets is changed.
///
/// Tools ▸ ClubPoker ▸ Localization ▸ Scan All Texts
///
/// Writes to &lt;project&gt;/Localization/Reports/ (outside Assets, so Unity
/// doesn't import the CSVs):
///   texts_all.csv     — every occurrence: text, where, component, translate?
///   texts_unique.csv  — each distinct text once, with use count + suggested key
/// </summary>
public static class LocalizationScanTool
{
    private const string REPORT_DIR = "Localization/Reports";

    // Folders that hold third-party / sample content, not our UI.
    private static readonly string[] SkipFolders =
    {
        "Assets/Plugins/", "Assets/TextMesh Pro/", "Assets/Samples/", "Assets/ThirdParty/",
    };

    // Unity defaults left on objects — never player-visible copy.
    private static readonly HashSet<string> DefaultTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "New Text", "Text", "Button", "Option A", "Option B", "Option C",
        "Enter text...", "Placeholder", "Label", "Toggle", "Dropdown",
    };

    private class Entry
    {
        public string Text;
        public string Source;      // scene / prefab / script path
        public string Location;    // object path or line number
        public string Kind;        // TMP, Text, Code, GameMessages
        public string Translate;   // Yes, or the reason it's skipped
    }

    [MenuItem("Tools/ClubPoker/Localization/Scan All Texts")]
    public static void ScanAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var entries = new List<Entry>();
        string[] openScenes = Enumerable.Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i).path).ToArray();

        try
        {
            ScanPrefabs(entries);
            ScanScenes(entries, openScenes);
            ScanCode(entries);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        WriteReports(entries);
    }

    // ── Prefabs ─────────────────────────────────────────────────────────────

    private static void ScanPrefabs(List<Entry> entries)
    {
        string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !IsSkipped(p))
            .ToArray();

        for (int i = 0; i < paths.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Scanning prefabs", paths[i], (float)i / paths.Length);

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (root != null)
                CollectTexts(root, paths[i], entries);
        }
    }

    // ── Scenes ──────────────────────────────────────────────────────────────

    private static void ScanScenes(List<Entry> entries, string[] alreadyOpen)
    {
        string[] paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !IsSkipped(p))
            .ToArray();

        for (int i = 0; i < paths.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Scanning scenes", paths[i], (float)i / paths.Length);

            bool wasOpen = alreadyOpen.Contains(paths[i]);
            Scene scene = wasOpen
                ? SceneManager.GetSceneByPath(paths[i])
                : EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Additive);

            foreach (GameObject root in scene.GetRootGameObjects())
                CollectTexts(root, paths[i], entries);

            if (!wasOpen)
                EditorSceneManager.CloseScene(scene, true);   // close, no save
        }
    }

    private static void CollectTexts(GameObject root, string source, List<Entry> entries)
    {
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            entries.Add(Make(t.text, source, PathOf(t.transform), "TMP"));

        foreach (Text t in root.GetComponentsInChildren<Text>(true))
            entries.Add(Make(t.text, source, PathOf(t.transform), "Text"));

        // Dropdown option labels live in the component, not in a text object.
        foreach (TMP_Dropdown d in root.GetComponentsInChildren<TMP_Dropdown>(true))
            foreach (var o in d.options)
                entries.Add(Make(o.text, source, PathOf(d.transform) + " [option]", "TMP Dropdown"));

        foreach (Dropdown d in root.GetComponentsInChildren<Dropdown>(true))
            foreach (var o in d.options)
                entries.Add(Make(o.text, source, PathOf(d.transform) + " [option]", "Dropdown"));
    }

    // ── Code ────────────────────────────────────────────────────────────────

    // Literal strings that end up on screen: assigned to .text, shown as a
    // toast / error, or declared in GameMessages.
    private static readonly Regex[] CodePatterns =
    {
        new Regex(@"\.text\s*=\s*\$?""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled),
        new Regex(@"ToastEvents\.Show\(\s*\$?""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled),
        new Regex(@"Show\w*(?:Error|Message|Toast|Popup)\w*\(\s*\$?""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled),
    };

    private static readonly Regex GameMessagesConst =
        new Regex(@"public\s+const\s+string\s+\w+\s*=\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

    private static readonly Regex GameMessagesFormat =
        new Regex(@"public\s+static\s+string\s+\w+\([^)]*\)\s*=>\s*\$""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

    private static void ScanCode(List<Entry> entries)
    {
        string[] files = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
            .Select(f => f.Replace('\\', '/'))
            .Where(f => !f.Contains("/Tests/"))
            .ToArray();

        foreach (string file in files)
        {
            bool isMessages = file.EndsWith("/GameMessages.cs");
            string[] lines = File.ReadAllLines(file);

            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n];
                if (line.TrimStart().StartsWith("//"))
                    continue;

                if (isMessages)
                {
                    foreach (Match m in GameMessagesConst.Matches(line))
                        entries.Add(Make(Unescape(m.Groups[1].Value), file, "line " + (n + 1), "GameMessages"));
                    foreach (Match m in GameMessagesFormat.Matches(line))
                        entries.Add(Make(Unescape(m.Groups[1].Value), file, "line " + (n + 1), "GameMessages"));
                    continue;
                }

                foreach (Regex rx in CodePatterns)
                    foreach (Match m in rx.Matches(line))
                        entries.Add(Make(Unescape(m.Groups[1].Value), file, "line " + (n + 1), "Code"));
            }
        }
    }

    // ── Classification ──────────────────────────────────────────────────────

    private static Entry Make(string text, string source, string location, string kind) => new Entry
    {
        Text = text ?? string.Empty,
        Source = source,
        Location = location,
        Kind = kind,
        Translate = Classify(text),
    };

    private static string Classify(string raw)
    {
        string t = (raw ?? string.Empty).Replace("​", "").Trim();

        if (t.Length == 0)                         return "No: empty";
        if (DefaultTexts.Contains(t))              return "No: Unity default";
        if (!t.Any(char.IsLetter))                 return "No: numbers/symbols";
        if (t.Length == 1)                         return "No: single character";
        if (Regex.IsMatch(t, @"^\{[^}]*\}$"))      return "No: placeholder only";
        if (Regex.IsMatch(t, @"^[\d\s.,:%$+\-xXkKmMbB/]+$")) return "No: number/amount";
        if (t.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return "No: URL";
        return "Yes";
    }

    // ── Output ──────────────────────────────────────────────────────────────

    private static void WriteReports(List<Entry> entries)
    {
        Directory.CreateDirectory(REPORT_DIR);
        string allPath = Path.Combine(REPORT_DIR, "texts_all.csv");
        string uniquePath = Path.Combine(REPORT_DIR, "texts_unique.csv");

        var all = new StringBuilder("Text,Translate,Kind,Source,Location\n");
        foreach (Entry e in entries.OrderBy(e => e.Source).ThenBy(e => e.Location))
            all.Append(Csv(e.Text)).Append(',').Append(Csv(e.Translate)).Append(',')
               .Append(Csv(e.Kind)).Append(',').Append(Csv(e.Source)).Append(',')
               .Append(Csv(e.Location)).Append('\n');
        File.WriteAllText(allPath, all.ToString(), new UTF8Encoding(true));   // BOM so Excel reads UTF-8

        var groups = entries
            .Where(e => e.Translate == "Yes")
            .GroupBy(e => e.Text.Trim())
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .ToList();

        var keysUsed = new HashSet<string>();
        var unique = new StringBuilder("SuggestedKey,English,Uses,Kinds,UsedIn\n");
        foreach (var g in groups)
        {
            string key = UniqueKey(SuggestKey(g.First(), g.Key), keysUsed);
            string kinds = string.Join(" ", g.Select(e => e.Kind).Distinct());
            var places = g.Select(e => Path.GetFileNameWithoutExtension(e.Source)).Distinct().ToList();
            string usedIn = string.Join("; ", places.Take(6)) + (places.Count > 6 ? $"; +{places.Count - 6} more" : "");

            unique.Append(Csv(key)).Append(',').Append(Csv(g.Key)).Append(',').Append(g.Count())
                  .Append(',').Append(Csv(kinds)).Append(',').Append(Csv(usedIn)).Append('\n');
        }
        File.WriteAllText(uniquePath, unique.ToString(), new UTF8Encoding(true));

        int translate = entries.Count(e => e.Translate == "Yes");
        string summary =
            $"Scanned {entries.Count} texts — {translate} to translate, {groups.Count} distinct strings.\n" +
            $"UI: {entries.Count(e => e.Kind.StartsWith("TMP") || e.Kind.StartsWith("Text") || e.Kind.Contains("Dropdown"))}, " +
            $"code: {entries.Count(e => e.Kind == "Code")}, GameMessages: {entries.Count(e => e.Kind == "GameMessages")}.\n\n" +
            $"Reports in {Path.GetFullPath(REPORT_DIR)}";

        Debug.Log("[Localization] " + summary.Replace("\n", " "));
        if (EditorUtility.DisplayDialog("Text scan done", summary, "Open folder", "Close"))
            EditorUtility.RevealInFinder(uniquePath);
    }

    // area.prefab_or_scene.text_slug — e.g. ui.hamburgermenu.language
    private static string SuggestKey(Entry e, string text)
    {
        string area = e.Kind == "GameMessages" ? "msg" : FirstFolder(e.Source);
        string owner = Slug(Path.GetFileNameWithoutExtension(e.Source), 20);
        string slug = Slug(text, 30);
        return e.Kind == "GameMessages" ? $"msg.{slug}" : $"{area}.{owner}.{slug}";
    }

    private static string UniqueKey(string key, HashSet<string> used)
    {
        string k = key;
        for (int i = 2; !used.Add(k); i++)
            k = $"{key}_{i}";
        return k;
    }

    private static string FirstFolder(string path)
    {
        // Assets/Prefabs/Club/X.prefab → club ; Assets/Scenes/X.unity → scene ; Assets/Scripts/UI/X.cs → ui
        string[] parts = path.Split('/');
        if (path.EndsWith(".unity")) return "scene";
        if (parts.Length >= 4 && (parts[1] == "Prefabs" || parts[1] == "Scripts")) return Slug(parts[2], 12);
        return parts.Length >= 2 ? Slug(parts[1], 12) : "misc";
    }

    private static string Slug(string s, int max)
    {
        string slug = Regex.Replace((s ?? "").ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        if (slug.Length > max) slug = slug.Substring(0, max).TrimEnd('_');
        return slug.Length == 0 ? "text" : slug;
    }

    private static string PathOf(Transform t)
    {
        var names = new List<string>();
        for (; t != null; t = t.parent)
            names.Add(t.name);
        names.Reverse();
        return string.Join(" > ", names);
    }

    private static bool IsSkipped(string path) =>
        SkipFolders.Any(f => path.StartsWith(f, StringComparison.OrdinalIgnoreCase));

    private static string Unescape(string s) =>
        s.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");

    private static string Csv(string s)
    {
        s = (s ?? string.Empty).Replace("​", "");
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
