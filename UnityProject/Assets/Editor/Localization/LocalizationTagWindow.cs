using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClubPoker.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tags the fixed texts of the open scene — or the open prefab, in prefab mode —
/// with LocalizedText, and writes their English into en.json.
///
/// Tools ▸ ClubPoker ▸ Localization ▸ Tag Texts (Open Scene or Prefab)
///
/// Nothing changes until Apply. Every row can be ticked/unticked and its key
/// edited. Same English → same key ("Confirm" everywhere is "confirm"); keys
/// already in en.json are reused. Texts inside a prefab are tagged in that
/// prefab (Open button), not per scene copy. Apply is one Undo step.
/// </summary>
internal class LocalizationTagWindow : EditorWindow
{
    private enum State { New, Reuse, Conflict, Tagged, InPrefab, Skipped }

    private class Row
    {
        public Component Comp;
        public string English;
        public string Key;
        public bool Include;
        public State State;
        public string Note;
        public string Path;
        public string Kind;
        public GameObject PrefabAsset;
    }

    // Key naming:
    //   short text (≤ 4 words, ≤ 28 chars) → the text itself: "Forgot password?" → forgot_password
    //   long text                           → where it's used: <scene/prefab>.<object>, e.g. login.popupdesc
    private const int ShortMaxWords = 4;
    private const int ShortMaxChars = 28;

    // Object names that say nothing about the text — use the parent's name instead.
    private static readonly HashSet<string> GenericNames = new HashSet<string>
    {
        "text", "text_tmp", "tmp", "label", "title", "txt", "value", "content",
        "message", "msg", "desc", "description", "name", "header", "body",
    };

    private readonly List<Row> _rows = new List<Row>();
    private SortedDictionary<string, string> _english;
    private string _contextName = "";
    private bool _isPrefab;
    private Vector2 _scroll;
    private bool _showSkipped;
    private bool _showTagged;

    [MenuItem("Tools/ClubPoker/Localization/Tag Texts (Open Scene or Prefab)")]
    private static void Open()
    {
        var window = GetWindow<LocalizationTagWindow>("Tag Texts");
        window.minSize = new Vector2(760, 400);
        window.Rescan();
    }

    // ── Scan ────────────────────────────────────────────────────────────────

    private void Rescan()
    {
        _rows.Clear();
        _english = LocalizationFiles.Load("en");

        var keyByEnglish = new Dictionary<string, string>();
        foreach (var kv in _english)
        {
            string mk = LocalizationFiles.MatchKey(kv.Value);
            if (!keyByEnglish.ContainsKey(mk))
                keyByEnglish[mk] = kv.Key;
        }

        GameObject[] roots;
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            roots = new[] { stage.prefabContentsRoot };
            _contextName = Path.GetFileNameWithoutExtension(stage.assetPath);
            _isPrefab = true;
        }
        else
        {
            Scene scene = SceneManager.GetActiveScene();
            roots = scene.GetRootGameObjects();
            _contextName = scene.name;
            _isPrefab = false;
        }

        string area = LocalizationScanTool.Slug(_contextName.Replace("Scene", ""), 16);

        var referenced = new HashSet<Component>();
        foreach (GameObject root in roots)
            referenced.UnionWith(LocalizationScanTool.TextsReferencedByScripts(root));

        var usedKeys = new HashSet<string>(_english.Keys);
        var newKeyByEnglish = new Dictionary<string, string>();

        foreach (GameObject root in roots)
        {
            IEnumerable<Component> texts = root.GetComponentsInChildren<TMP_Text>(true).Cast<Component>()
                .Concat(root.GetComponentsInChildren<Text>(true));

            foreach (Component comp in texts)
            {
                string text = TextOf(comp);
                var row = new Row
                {
                    Comp = comp,
                    English = LocalizationScanTool.Normalize(text),
                    Path = PathOf(comp.transform),
                    Kind = comp is TMP_Text ? "TMP" : "Text",
                };
                _rows.Add(row);

                var tagged = comp.GetComponent<LocalizedText>();
                if (tagged != null)
                {
                    row.State = State.Tagged;
                    row.Key = tagged.Key;
                    row.Note = "already tagged";
                    continue;
                }

                if (LocalizationScanTool.IsUnchangedPrefabCopy(comp, text))
                {
                    row.State = State.InPrefab;
                    row.PrefabAsset = SourcePrefab(comp);
                    row.Note = "tag in prefab " + (row.PrefabAsset != null ? row.PrefabAsset.name : "?");
                    continue;
                }

                string verdict = LocalizationScanTool.Classify(text);
                if (verdict.StartsWith("No"))
                {
                    row.State = State.Skipped;
                    row.Note = verdict.Substring(4);
                    continue;
                }

                string match = LocalizationFiles.MatchKey(text);
                if (keyByEnglish.TryGetValue(match, out string existing))
                {
                    row.Key = existing;
                    row.State = State.Reuse;
                }
                else
                {
                    if (!newKeyByEnglish.TryGetValue(match, out string key))
                    {
                        key = SuggestKey(area, text, comp.transform, usedKeys);
                        usedKeys.Add(key);
                        newKeyByEnglish[match] = key;
                    }
                    row.Key = key;
                    row.State = State.New;
                }

                bool byCode = referenced.Contains(comp);
                row.Include = verdict == LocalizationScanTool.YES && !byCode;
                row.Note = byCode ? "a script references it — set by code?" :
                           verdict == LocalizationScanTool.CHECK_SHORT ? "very short" : "";
            }
        }

        _rows.Sort((a, b) => Order(a.State).CompareTo(Order(b.State)));
        Repaint();
    }

    private static int Order(State s) =>
        s == State.Conflict ? 0 : s == State.New ? 1 : s == State.Reuse ? 2 :
        s == State.InPrefab ? 3 : s == State.Tagged ? 4 : 5;

    private static string SuggestKey(string area, string text, Transform where, HashSet<string> used)
    {
        string clean = LocalizationScanTool.Normalize(text);
        int words = clean.Split(' ').Length;

        string baseKey = words <= ShortMaxWords && clean.Length <= ShortMaxChars
            ? LocalizationScanTool.Slug(clean, ShortMaxChars)
            : $"{area}.{LocalizationScanTool.Slug(MeaningfulName(where), 24)}";

        string key = baseKey;
        for (int i = 2; used.Contains(key); i++)
            key = $"{baseKey}_{i}";
        return key;
    }

    // First name up the hierarchy that isn't generic: "Text (TMP)" under
    // "ForgotPasswordInfo" → "ForgotPasswordInfo".
    private static string MeaningfulName(Transform t)
    {
        for (Transform cur = t; cur != null; cur = cur.parent)
        {
            // "Text (TMP) (1)" → "text_tmp"
            string n = LocalizationScanTool.Slug(System.Text.RegularExpressions.Regex.Replace(cur.name, @"\s*\(\d+\)$", ""), 40);
            if (!GenericNames.Contains(n))
                return cur.name;
        }
        return t.name;
    }

    // Re-check a row after its key was edited by hand.
    private void Evaluate(Row row)
    {
        if (string.IsNullOrWhiteSpace(row.Key))
        {
            row.State = State.Conflict;
            row.Note = "key is empty";
            return;
        }

        string mine = LocalizationFiles.MatchKey(row.English);

        if (_english.TryGetValue(row.Key, out string existing))
        {
            bool same = LocalizationFiles.MatchKey(existing) == mine;
            row.State = same ? State.Reuse : State.Conflict;
            row.Note = same ? "" : $"key already means \"{existing}\"";
            return;
        }

        Row clash = _rows.FirstOrDefault(r => r != row && r.Include && r.Key == row.Key &&
                                              LocalizationFiles.MatchKey(r.English) != mine);
        row.State = clash != null ? State.Conflict : State.New;
        row.Note = clash != null ? $"same key as \"{clash.English}\"" : "";
    }

    // ── Apply ───────────────────────────────────────────────────────────────

    private void Apply()
    {
        List<Row> rows = _rows.Where(r => r.Include && r.Comp != null &&
                                          (r.State == State.New || r.State == State.Reuse)).ToList();
        if (rows.Count == 0)
            return;

        int newKeys = rows.Where(r => r.State == State.New).Select(r => r.Key).Distinct().Count();
        if (!EditorUtility.DisplayDialog("Tag texts",
                $"Add LocalizedText to {rows.Count} texts in {_contextName}?\n" +
                $"{newKeys} new keys go into en.json.", "Apply", "Cancel"))
            return;

        var memory = LocalizationFiles.LoadMemory();
        var tables = LocalizationFiles.TranslatedCodes.ToDictionary(c => c, LocalizationFiles.Load);
        int fromMemory = 0;

        Undo.SetCurrentGroupName("Tag texts for localization");
        int group = Undo.GetCurrentGroup();

        foreach (Row row in rows)
        {
            var lt = Undo.AddComponent<LocalizedText>(row.Comp.gameObject);
            var so = new SerializedObject(lt);
            so.FindProperty("key").stringValue = row.Key;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.IsPartOfPrefabInstance(lt))
                PrefabUtility.RecordPrefabInstancePropertyModifications(lt);

            if (_english.ContainsKey(row.Key))
                continue;

            string english = TextOf(row.Comp).Replace("​", "").Trim();
            _english[row.Key] = english;

            // Translated earlier from the scan sheet (Import by English)?
            if (memory.TryGetValue(LocalizationFiles.MatchKey(english), out var langs))
                foreach (var kv in langs)
                    if (tables.TryGetValue(kv.Key, out var table) && !table.ContainsKey(row.Key))
                    {
                        table[row.Key] = kv.Value;
                        fromMemory++;
                    }
        }

        Undo.CollapseUndoOperations(group);

        LocalizationFiles.Save("en", _english);
        foreach (var kv in tables)
            LocalizationFiles.Save(kv.Key, kv.Value);

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        EditorSceneManager.MarkSceneDirty(stage != null ? stage.scene : SceneManager.GetActiveScene());

        Debug.Log($"[Localization] Tagged {rows.Count} texts in {_contextName}; {newKeys} new keys" +
                  (fromMemory > 0 ? $"; {fromMemory} translations filled from the imported sheet." : "."));
        Rescan();
    }

    // ── UI ──────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label((_isPrefab ? "Prefab: " : "Scene: ") + _contextName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _showTagged = GUILayout.Toggle(_showTagged, "Show tagged", EditorStyles.toolbarButton);
            _showSkipped = GUILayout.Toggle(_showSkipped, "Show skipped", EditorStyles.toolbarButton);
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton))
                Rescan();
        }

        int CountOf(State s) => _rows.Count(r => r.State == s);
        EditorGUILayout.HelpBox(
            $"New {CountOf(State.New)}   Reuse {CountOf(State.Reuse)}   Conflict {CountOf(State.Conflict)}   " +
            $"In prefab {CountOf(State.InPrefab)}   Tagged {CountOf(State.Tagged)}   Skipped {CountOf(State.Skipped)}\n" +
            "Ticked = gets LocalizedText. Untick dynamic texts (names, amounts). Opened another scene or prefab? Press Rescan.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tick all New/Reuse", GUILayout.Width(140)))
                foreach (Row r in _rows.Where(r => r.State == State.New || r.State == State.Reuse)) r.Include = true;
            if (GUILayout.Button("Untick all", GUILayout.Width(90)))
                foreach (Row r in _rows) r.Include = false;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (Row row in _rows)
        {
            if (row.Comp == null) continue;
            if (row.State == State.Skipped && !_showSkipped) continue;
            if (row.State == State.Tagged && !_showTagged) continue;
            DrawRow(row);
        }
        EditorGUILayout.EndScrollView();

        int ready = _rows.Count(r => r.Include && (r.State == State.New || r.State == State.Reuse));
        using (new EditorGUI.DisabledScope(ready == 0))
            if (GUILayout.Button($"Apply ({ready})", GUILayout.Height(28)))
                Apply();
    }

    private void DrawRow(Row row)
    {
        bool editable = row.State == State.New || row.State == State.Reuse || row.State == State.Conflict;

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!editable || row.State == State.Conflict))
                row.Include = EditorGUILayout.Toggle(row.Include && row.State != State.Conflict, GUILayout.Width(16));

            EditorGUILayout.LabelField(new GUIContent(row.English, row.Path), GUILayout.Width(230));

            using (new EditorGUI.DisabledScope(!editable))
            {
                EditorGUI.BeginChangeCheck();
                row.Key = EditorGUILayout.TextField(row.Key ?? "", GUILayout.Width(220));
                if (EditorGUI.EndChangeCheck())
                    Evaluate(row);
            }

            var old = GUI.color;
            GUI.color = row.State == State.Conflict ? new Color(1f, 0.5f, 0.5f) :
                        row.State == State.New ? new Color(0.6f, 1f, 0.6f) : old;
            GUILayout.Label(row.State.ToString(), GUILayout.Width(62));
            GUI.color = old;

            GUILayout.Label($"{row.Kind}  {row.Note}", EditorStyles.miniLabel, GUILayout.MinWidth(80));

            if (row.State == State.InPrefab && row.PrefabAsset != null)
            {
                if (GUILayout.Button("Open", GUILayout.Width(48)))
                    AssetDatabase.OpenAsset(row.PrefabAsset);
            }
            else if (GUILayout.Button("Select", GUILayout.Width(48)))
            {
                Selection.activeGameObject = row.Comp.gameObject;
                EditorGUIUtility.PingObject(row.Comp.gameObject);
            }
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string TextOf(Component c) => c switch
    {
        TMP_Text t => t.text ?? "",
        Text t => t.text ?? "",
        _ => "",
    };

    private static GameObject SourcePrefab(Component c)
    {
        Component source = PrefabUtility.GetCorrespondingObjectFromSource(c);
        string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static string PathOf(Transform t)
    {
        var names = new List<string>();
        for (; t != null; t = t.parent)
            names.Add(t.name);
        names.Reverse();
        return string.Join(" > ", names);
    }
}
