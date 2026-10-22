using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClubPoker.Core
{
    /// <summary>
    /// Put on a TMP or legacy Text object with a fixed label. Shows Loc.Get(key)
    /// and updates the moment the language changes.
    ///
    /// Not for texts code fills in at runtime (names, amounts) — those call
    /// Loc.Get in code, or this component would overwrite them.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;

        private TMP_Text _tmp;
        private Text _text;

        public string Key
        {
            get => key;
            set { key = value; Refresh(); }
        }

        private void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
            _text = GetComponent<Text>();
        }

        private void OnEnable()
        {
            Loc.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.OnLanguageChanged -= Refresh;
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(key))
                return;

            string value = Loc.Get(key);
            if (_tmp != null) _tmp.text = value;
            else if (_text != null) _text.text = value;
        }
    }
}
