using System.Collections.Generic;
using System.IO;
using ClubPoker.Theme;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time setup + migration helpers for the theme system.
/// Tools ▸ ClubPoker ▸ Theme
/// </summary>
public static class ThemeSetupTools
{
    private const string CATALOG_DIR  = "Assets/Resources/Theme";
    private const string CATALOG_PATH = CATALOG_DIR + "/ThemeCatalog.asset";

    [MenuItem("Tools/ClubPoker/Theme/Create Theme Catalog")]
    public static void CreateCatalog()
    {
        Directory.CreateDirectory(CATALOG_DIR);

        ThemeCatalogSO existing = AssetDatabase.LoadAssetAtPath<ThemeCatalogSO>(CATALOG_PATH);

        if (existing != null)
        {
            Selection.activeObject = existing;
            Debug.Log($"[Theme] Catalog already exists at {CATALOG_PATH}");
            return;
        }

        ThemeCatalogSO catalog = ScriptableObject.CreateInstance<ThemeCatalogSO>();
        AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
        AssetDatabase.SaveAssets();

        Selection.activeObject = catalog;
        Debug.Log($"[Theme] Created {CATALOG_PATH}");
    }

    /// <summary>
    /// Reads the inline sprite list off a selected prefab (any component with
    /// public Sprite CardBackSprite + a card sprite list) and writes it out as a
    /// CardDeckSO, so the shipped deck becomes the first catalog entry instead of
    /// being re-authored by hand.
    /// </summary>
    [MenuItem("Tools/ClubPoker/Theme/Extract Deck From Selected Prefab")]
    public static void ExtractDeck()
    {
        GameObject go = Selection.activeGameObject;

        if (go == null)
        {
            EditorUtility.DisplayDialog("Extract Deck",
                "Select a prefab holding a card sprite list (CardFlipPrefab, PlayerCardHandUI, PlayerProfile).",
                "OK");
            return;
        }

        List<CardFaceEntry> faces = new List<CardFaceEntry>();
        Sprite back = null;

        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null)
                continue;

            SerializedObject so = new SerializedObject(mb);

            SerializedProperty backProp = so.FindProperty("CardBackSprite");
            if (backProp != null && backProp.objectReferenceValue is Sprite s)
                back = s;

            SerializedProperty list = so.FindProperty("CardSprites");
            if (list == null || !list.isArray)
                continue;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);

                string name = element.FindPropertyRelative("CardName")?.stringValue;
                Sprite sprite = element.FindPropertyRelative("CardSprite")?.objectReferenceValue as Sprite;

                if (string.IsNullOrEmpty(name) || sprite == null)
                    continue;

                faces.Add(new CardFaceEntry { CardName = name, CardSprite = sprite });
            }
        }

        if (faces.Count == 0)
        {
            Debug.LogWarning($"[Theme] No card sprites found on {go.name}");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Card Deck", "CardDeck_Classic", "asset", "", "Assets/Resources/Theme");

        if (string.IsNullOrEmpty(path))
            return;

        CardDeckSO deck = ScriptableObject.CreateInstance<CardDeckSO>();
        deck.DeckId = Path.GetFileNameWithoutExtension(path);
        deck.DisplayName = deck.DeckId;
        deck.CardBackSprite = back;
        deck.Faces = faces;

        AssetDatabase.CreateAsset(deck, path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = deck;
        Debug.Log($"[Theme] Extracted {faces.Count} faces into {path}");
    }

    /// <summary>Flags decks missing any of the 52 faces before they ship.</summary>
    [MenuItem("Tools/ClubPoker/Theme/Validate Catalog")]
    public static void ValidateCatalog()
    {
        ThemeCatalogSO catalog = AssetDatabase.LoadAssetAtPath<ThemeCatalogSO>(CATALOG_PATH);

        if (catalog == null)
        {
            Debug.LogError($"[Theme] No catalog at {CATALOG_PATH}");
            return;
        }

        string ranks = "23456789TJQKA";
        string suits = "SHDC";
        int problems = 0;

        foreach (CardDeckSO deck in catalog.Decks)
        {
            if (deck == null)
                continue;

            if (string.IsNullOrEmpty(deck.DeckId))
            {
                Debug.LogError($"[Theme] Deck '{deck.name}' has no DeckId", deck);
                problems++;
            }

            if (deck.CardBackSprite == null)
            {
                Debug.LogError($"[Theme] Deck '{deck.name}' has no back sprite", deck);
                problems++;
            }

            List<string> missing = new List<string>();

            foreach (char r in ranks)
                foreach (char s in suits)
                    if (!deck.HasFace($"{r}{s}"))
                        missing.Add($"{r}{s}");

            if (missing.Count > 0)
            {
                Debug.LogError($"[Theme] Deck '{deck.name}' missing {missing.Count}: {string.Join(", ", missing)}", deck);
                problems++;
            }
        }

        foreach (TableSkinSO table in catalog.Tables)
            if (table != null && string.IsNullOrEmpty(table.TableId))
            {
                Debug.LogError($"[Theme] Table '{table.name}' has no TableId", table);
                problems++;
            }

        foreach (ThemeSO theme in catalog.Themes)
            if (theme != null && (theme.Table == null || theme.Deck == null))
            {
                Debug.LogError($"[Theme] Theme '{theme.name}' has an empty Table or Deck slot", theme);
                problems++;
            }

        Debug.Log(problems == 0
            ? "[Theme] Catalog valid."
            : $"[Theme] Catalog has {problems} problem(s).");
    }
}
