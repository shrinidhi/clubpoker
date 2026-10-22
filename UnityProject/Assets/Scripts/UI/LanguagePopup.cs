using System.Collections.Generic;
using ClubPoker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.UI
{
    /// <summary>
    /// Language picker. Rows are found by name under <see cref="rowsContainer"/>:
    /// a child named "Lang_es" is the Spanish row, and so on — add or remove a
    /// language by adding or removing a row (and its code in Loc.Languages).
    ///
    /// Tapping a row only selects it; Confirm applies, the X closes unchanged.
    /// Open it from any button's OnClick → LanguagePopup.Show.
    /// </summary>
    public class LanguagePopup : MonoBehaviour
    {
        private const string ROW_PREFIX = "Lang_";

        [SerializeField] private Transform rowsContainer;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;

        [Header("Selected row look")]
        [Tooltip("Optional. Sprite for the selected row's background; empty = tint with the colors below.")]
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(0.55f, 0.75f, 1f);
        [Tooltip("Optional. Name of a child object shown only on the selected row (e.g. a tick). " +
                 "Empty = leave children as they are.")]
        [SerializeField] private string selectedMarkerName = "";

        private class Row
        {
            public string Code;
            public Image Background;
            public Sprite NormalSprite;
            public GameObject Marker;
        }

        private readonly List<Row> _rows = new List<Row>();
        private string _pending;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(Apply);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            if (rowsContainer == null)
            {
                Debug.LogError("[LanguagePopup] Rows Container not set.");
                return;
            }

            foreach (Transform child in rowsContainer)
            {
                if (!child.name.StartsWith(ROW_PREFIX))
                    continue;

                string code = child.name.Substring(ROW_PREFIX.Length);
                if (!Loc.IsSupported(code))
                {
                    Debug.LogWarning($"[LanguagePopup] Row '{child.name}': '{code}' isn't in Loc.Languages — hidden.");
                    child.gameObject.SetActive(false);
                    continue;
                }

                var row = new Row
                {
                    Code = code,
                    Background = child.GetComponent<Image>(),
                    Marker = string.IsNullOrEmpty(selectedMarkerName) ? null : child.Find(selectedMarkerName)?.gameObject,
                };
                row.NormalSprite = row.Background != null ? row.Background.sprite : null;
                _rows.Add(row);

                if (child.TryGetComponent(out Button button))
                    button.onClick.AddListener(() => Select(code));
            }
        }

        private void OnEnable()
        {
            // Opening shows the language in use, not a leftover unconfirmed pick.
            _pending = Loc.Language;
            RefreshRows();
        }

        public void Show()
        {
            transform.SetAsLastSibling();   // above whatever opened it
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Select(string code)
        {
            _pending = code;
            RefreshRows();
        }

        private void Apply()
        {
            Loc.SetLanguage(_pending);   // no-op if unchanged
            Hide();
        }

        private void RefreshRows()
        {
            foreach (Row row in _rows)
            {
                bool selected = row.Code == _pending;

                if (row.Background != null)
                {
                    if (selectedSprite != null)
                    {
                        row.Background.sprite = selected ? selectedSprite : row.NormalSprite;
                        row.Background.color = normalColor;
                    }
                    else
                    {
                        row.Background.color = selected ? selectedColor : normalColor;
                    }
                }

                if (row.Marker != null)
                    row.Marker.SetActive(selected);
            }
        }
    }
}
