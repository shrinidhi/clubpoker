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
///   texts_to_translate.csv — fixed labels, each distinct text once + suggested key
///   texts_check.csv        — probably set by code at runtime (names, amounts…)
///                            or too short to judge; review by hand
///   texts_all.csv          — every occurrence, for tracing where a text lives
///
/// Noise it removes:
///   - Prefab copies: a prefab placed in a scene (or nested in another prefab)
///     is counted once, in its own prefab — unless the copy changed the text.
///   - Numbers / amounts / times ("1.5K", "x2", "00:30", "$100", "12/45").
///   - Unity defaults ("New Text", "Option A", "Enter text...").
///   - Case / spacing variants ("Confirm", "CONFIRM", "Confirm ") grouped.
///   - Texts a script holds a reference to are flagged "set by code": static
///     labels are rarely referenced, dynamic ones (names, chips) almost always.
/// </summary>
public static class LocalizationScanTool
{
    private const string REPORT_DIR = "Localization/Reports";

    internal const string YES = "Yes";
    internal const string CHECK_CODE = "Check: set by code";
    internal const string CHECK_SHORT = "Check: very short";

    // Folders that hold third-party / sample content, not our UI.
    private static readonly string[] SkipFolders =
    {
        "Assets/Plugins/", "Assets/TextMesh Pro/", "Assets/Samples/", "Assets/ThirdParty/",
    };

    // Unity defaults and filler left on objects — never player-visible copy.
    private static readonly HashSet<string> DefaultTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "New Text", "Text", "Button", "Option A", "Option B", "Option C",
        "Enter text...", "Placeholder", "Label", "Toggle", "Dropdown", "Item Label",
        "Lorem ipsum", "Sample Text", "Text (TMP)", "Text (Legacy)",
    };

    private class Entry
    {
        public string Text;
        public string Source;      // scene / prefab / script path
        public string Location;    // object path or line number
        public string Kind;        // TMP, Text, Code, GameMessages
        public string Translate;   // Yes, Check: …, or No: reason
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
        HashSet<Component> referenced = TextsReferencedByScripts(root);

        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            if (!IsUnchangedPrefabCopy(t, t.text))
                entries.Add(Make(t.text, source, PathOf(t.transform), "TMP", referenced.Contains(t)));

        foreach (Text t in root.GetComponentsInChildren<Text>(true))
            if (!IsUnchangedPrefabCopy(t, t.text))
                entries.Add(Make(t.text, source, PathOf(t.transform), "Text", referenced.Contains(t)));

        // Dropdown option labels live in the component, not in a text object.
        foreach (TMP_Dropdown d in root.GetComponentsInChildren<TMP_Dropdown>(true))
            if (!PrefabUtility.IsPartOfPrefabInstance(d))
                foreach (var o in d.options)
                    entries.Add(Make(o.text, source, PathOf(d.transform) + " [option]", "TMP Dropdown", false));

        foreach (Dropdown d in root.GetComponentsInChildren<Dropdown>(true))
            if (!PrefabUtility.IsPartOfPrefabInstance(d))
                foreach (var o in d.options)
                    entries.Add(Make(o.text, source, PathOf(d.transform) + " [option]", "Dropdown", false));
    }

    /// <summary>
    /// A text inside a prefab instance whose value matches the prefab it came
    /// from — that prefab is scanned on its own, so this copy is a duplicate.
    /// </summary>
    internal static bool IsUnchangedPrefabCopy(Component c, string text)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(c))
            return false;

        Component original = PrefabUtility.GetCorrespondingObjectFromSource(c);
        string originalText = original switch
        {
            TMP_Text t => t.text,
            Text t => t.text,
            _ => null,
        };
        return originalText != null && originalText == text;
    }

    /// <summary>
    /// Text components that any of our scripts hold a serialized reference to
    /// (directly or via their GameObject). Those are almost always filled in at
    /// runtime — player names, chip amounts, timers.
    /// </summary>
    internal static HashSet<Component> TextsReferencedByScripts(GameObject root)
    {
        var result = new HashSet<Component>();

        foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null)
                continue;   // missing script

            // UGUI / TMP's own components (Button, InputField, Dropdown, layout…)
            // wire texts internally — that doesn't mean code sets them.
            string ns = mb.GetType().Namespace ?? string.Empty;
            if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro"))
                continue;

            var so = new SerializedObject(mb);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null)
                    continue;

                switch (p.objectReferenceValue)
                {
                    case TMP_Text t: result.Add(t); break;
                    case Text t: result.Add(t); break;
                    case GameObject go:
                        if (go.TryGetComponent(out TMP_Text gt)) result.Add(gt);
                        if (go.TryGetComponent(out Text lt)) result.Add(lt);
                        break;
                }
            }
        }

        return result;
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
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.Contains("Debug.Log"))
                    continue;

                if (isMessages)
                {
                    foreach (Match m in GameMessagesConst.Matches(line))
                        entries.Add(Make(Unescape(m.Groups[1].Value), file, "line " + (n + 1), "GameMessages", false));
                    foreach (Match m in GameMessagesFormat.Matches(line))
                        entries.Add(Make(Unescape(m.Groups[1].Value), file, "line " + (n + 1), "GameMessages", false));
                    continue;
                }

                foreach (Regex rx in CodePatterns)
                    foreach (Match m in rx.Matches(line))
                        entries.Add(Make(Unescape(m.Groups[1].Value), file, "line " + (n + 1), "Code", false));
            }
        }
    }

    // ── Classification ──────────────────────────────────────────────────────

    private static Entry Make(string text, string source, string location, string kind, bool referencedByScript)
    {
        string verdict = Classify(text);
        if (verdict == YES && referencedByScript)
            verdict = CHECK_CODE;

        return new Entry
        {
            Text = text ?? string.Empty,
            Source = source,
            Location = location,
            Kind = kind,
            Translate = verdict,
        };
    }

    private static readonly Regex TimeOrDate = new Regex(@"^\d{1,2}[:/.\-]\d{1,2}([:/.\-]\d{1,4})?$", RegexOptions.Compiled);

    internal static string Classify(string raw)
    {
        string t = Normalize(raw);

        if (t.Length == 0)                    return "No: empty";
        if (DefaultTexts.Contains(t))         return "No: Unity default";
        if (t.StartsWith("http", StringComparison.OrdinalIgnoreCase) || t.Contains("www.")) return "No: URL";
        if (Regex.IsMatch(t, @"^\S+@\S+\.\S+$")) return "No: email";
        if (Regex.IsMatch(t, @"^\{[^}]*\}$"))  return "No: placeholder only";
        if (TimeOrDate.IsMatch(t))            return "No: time/date";

        int letters = t.Count(char.IsLetter);
        int digits = t.Count(char.IsDigit);

        if (letters == 0)                     return "No: numbers/symbols";
        // "1.5K", "x2", "$100", "Lv 5", "100 BB", "#12": a number with a unit.
        if (digits > 0 && letters <= 3)       return "No: number/amount";
        if (letters == 1)                     return "No: single character";
        if (letters <= 2)                     return CHECK_SHORT;   // "OK", "BB", "VS"
        return YES;
    }

    internal static string Normalize(string raw) =>
        Regex.Replace((raw ?? string.Empty).Replace("​", ""), @"\s+", " ").Trim();

    // ── Output ──────────────────────────────────────────────────────────────

    private static void WriteReports(List<Entry> entries)
    {
        Directory.CreateDirectory(REPORT_DIR);

        // Every occurrence.
        var all = new StringBuilder("Text,Translate,Kind,Source,Location\n");
        foreach (Entry e in entries.OrderBy(e => e.Source).ThenBy(e => e.Location))
            all.Append(Csv(e.Text)).Append(',').Append(Csv(e.Translate)).Append(',')
               .Append(Csv(e.Kind)).Append(',').Append(Csv(e.Source)).Append(',')
               .Append(Csv(e.Location)).Append('\n');
        Write("texts_all.csv", all);

        int toTranslate = WriteGrouped("texts_to_translate.csv", entries.Where(e => e.Translate == YES), withKey: true);
        int toCheck = WriteGrouped("texts_check.csv", entries.Where(e => e.Translate.StartsWith("Check")), withKey: false);

        string summary =
            $"To translate: {toTranslate} distinct texts\n" +
            $"To check by hand: {toCheck} (probably set by code, or very short)\n" +
            $"Skipped: {entries.Count(e => e.Translate.StartsWith("No"))} (empty, numbers, defaults…)\n\n" +
            $"Reports in {Path.GetFullPath(REPORT_DIR)}";

        Debug.Log("[Localization] " + summary.Replace("\n", " "));
        if (EditorUtility.DisplayDialog("Text scan done", summary, "Open folder", "Close"))
            EditorUtility.RevealInFinder(Path.Combine(REPORT_DIR, "texts_to_translate.csv"));
    }

    /// <summary>
    /// One row per distinct text. "Confirm", "CONFIRM" and "Confirm " are one
    /// row (case and spacing ignored); the most used spelling is shown and the
    /// others listed as variants.
    /// </summary>
    private static int WriteGrouped(string fileName, IEnumerable<Entry> rows, bool withKey)
    {
        var groups = rows
            .GroupBy(e => Normalize(e.Text).ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .ToList();

        var keysUsed = new HashSet<string>();
        var sb = new StringBuilder(withKey
            ? "SuggestedKey,English,Uses,Variants,Kinds,UsedIn\n"
            : "Text,Reason,Uses,Kinds,UsedIn\n");

        foreach (var g in groups)
        {
            var spellings = g.GroupBy(e => Normalize(e.Text)).OrderByDescending(s => s.Count()).ToList();
            string text = spellings[0].Key;
            string variants = string.Join(" | ", spellings.Skip(1).Select(s => s.Key));
            string kinds = string.Join(" ", g.Select(e => e.Kind).Distinct());
            var places = g.Select(e => Path.GetFileNameWithoutExtension(e.Source)).Distinct().ToList();
            string usedIn = string.Join("; ", places.Take(6)) + (places.Count > 6 ? $"; +{places.Count - 6} more" : "");

            if (withKey)
            {
                string key = UniqueKey(SuggestKey(g.First(), text), keysUsed);
                sb.Append(Csv(key)).Append(',').Append(Csv(text)).Append(',').Append(g.Count()).Append(',')
                  .Append(Csv(variants)).Append(',');
            }
            else
            {
                sb.Append(Csv(text)).Append(',').Append(Csv(g.First().Translate)).Append(',')
                  .Append(g.Count()).Append(',');
            }
            sb.Append(Csv(kinds)).Append(',').Append(Csv(usedIn)).Append('\n');
        }

        Write(fileName, sb);
        return groups.Count;
    }

    private static void Write(string fileName, StringBuilder content) =>
        File.WriteAllText(Path.Combine(REPORT_DIR, fileName), content.ToString(), new UTF8Encoding(true)); // BOM: Excel reads UTF-8

    // area.prefab_or_scene.text_slug — e.g. ui.hamburgermenu.language
    private static string SuggestKey(Entry e, string text)
    {
        if (e.Kind == "GameMessages")
            return $"msg.{Slug(text, 30)}";

        string area = FirstFolder(e.Source);
        string owner = Slug(Path.GetFileNameWithoutExtension(e.Source), 20);
        return $"{area}.{owner}.{Slug(text, 30)}";
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

    internal static string Slug(string s, int max)
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
