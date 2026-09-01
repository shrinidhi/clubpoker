using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClubPoker.Theme;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds a new CardDeckSO from a sprite sheet by slice order, using an existing
/// deck as the naming reference. Only works when the new sheet is sliced in the
/// same order as the reference sheet — which is what to ask the artist for.
/// </summary>
public class DeckFromSheetWindow : EditorWindow
{
    private CardDeckSO _reference;
    private Texture2D _sheet;
    private string _deckId = "neon";
    private string _displayName = "Neon";

    [MenuItem("Tools/ClubPoker/Theme/New Deck From Sprite Sheet")]
    private static void Open()
    {
        GetWindow<DeckFromSheetWindow>(true, "New Deck From Sheet").minSize = new Vector2(420, 220);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Maps the new sheet's slices onto the reference deck's card names by " +
            "slice index. The new sheet must be sliced in the same order as the " +
            "reference sheet, with the same slice count.",
            MessageType.Info);

        _reference   = (CardDeckSO)EditorGUILayout.ObjectField("Reference Deck", _reference, typeof(CardDeckSO), false);
        _sheet       = (Texture2D)EditorGUILayout.ObjectField("New Sheet", _sheet, typeof(Texture2D), false);
        _deckId      = EditorGUILayout.TextField("Deck Id", _deckId);
        _displayName = EditorGUILayout.TextField("Display Name", _displayName);

        GUI.enabled = _reference != null && _sheet != null && !string.IsNullOrEmpty(_deckId);

        if (GUILayout.Button("Create Deck", GUILayout.Height(30)))
            Build();

        GUI.enabled = true;
    }

    private void Build()
    {
        // Reference faces, ordered by the slice index of their current sprite, so
        // "which name sits at slice N" is known.
        List<CardFaceEntry> refFaces = _reference.Faces
            .Where(f => f != null && f.CardSprite != null)
            .OrderBy(f => SliceIndex(f.CardSprite.name))
            .ToList();

        Sprite[] newSlices = AssetDatabase
            .LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(_sheet))
            .OfType<Sprite>()
            .OrderBy(s => SliceIndex(s.name))
            .ToArray();

        if (newSlices.Length == 0)
        {
            EditorUtility.DisplayDialog("New Deck",
                "That texture has no sprites. Set Sprite Mode = Multiple and slice it first.", "OK");
            return;
        }

        if (newSlices.Length < refFaces.Count)
        {
            EditorUtility.DisplayDialog("New Deck",
                $"Sheet has {newSlices.Length} slices but the reference deck needs {refFaces.Count} faces.",
                "OK");
            return;
        }

        // Reference back sprite's slice index tells which slice is the back.
        int backIndex = _reference.CardBackSprite != null
            ? SliceIndex(_reference.CardBackSprite.name)
            : -1;

        List<CardFaceEntry> faces = new List<CardFaceEntry>(refFaces.Count);

        for (int i = 0; i < refFaces.Count; i++)
        {
            int slice = SliceIndex(refFaces[i].CardSprite.name);
            Sprite sprite = FindBySliceIndex(newSlices, slice) ?? newSlices[i];

            faces.Add(new CardFaceEntry
            {
                CardName = refFaces[i].CardName,
                CardSprite = sprite
            });
        }

        string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(_reference));
        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/CardDeck_{_displayName}.asset");

        CardDeckSO deck = ScriptableObject.CreateInstance<CardDeckSO>();
        deck.DeckId = _deckId;
        deck.DisplayName = _displayName;
        deck.Faces = faces;
        deck.CardBackSprite = backIndex >= 0
            ? FindBySliceIndex(newSlices, backIndex)
            : null;

        AssetDatabase.CreateAsset(deck, path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = deck;
        Debug.Log($"[Theme] Created {path} with {faces.Count} faces. " +
                  "Run Validate Catalog after adding it to the catalog.");

        Close();
    }

    /// <summary>Trailing "_12" of a sliced sprite name; -1 when absent.</summary>
    private static int SliceIndex(string spriteName)
    {
        int underscore = spriteName.LastIndexOf('_');

        if (underscore < 0 || underscore == spriteName.Length - 1)
            return -1;

        return int.TryParse(spriteName.Substring(underscore + 1), out int index) ? index : -1;
    }

    private static Sprite FindBySliceIndex(Sprite[] slices, int index)
    {
        foreach (Sprite s in slices)
            if (SliceIndex(s.name) == index)
                return s;

        return null;
    }
}
