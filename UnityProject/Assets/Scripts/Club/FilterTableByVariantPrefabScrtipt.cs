using System;
using UnityEngine;
using UnityEngine.UI;

public class FilterTableByVariantPrefabScrtipt : MonoBehaviour
{
    public Button Variant_Button;
    public Text Variant_Name;

    public Image Button_BG;
    public Color NormalColor = Color.white;
    public Color SelectedColor = Color.white;

    public string VariantKey;
    private Action<string, FilterTableByVariantPrefabScrtipt> onClickCallback;

    public void SetData(
        string variantKey,
        string displayName,
        Action<string, FilterTableByVariantPrefabScrtipt> callback)
    {
        VariantKey = variantKey;
        onClickCallback = callback;

        if (Variant_Name != null)
            Variant_Name.text = displayName;

        if (Button_BG == null && Variant_Button != null)
            Button_BG = Variant_Button.GetComponent<Image>();

        if (Variant_Button != null)
        {
            Variant_Button.onClick.RemoveAllListeners();
            Variant_Button.onClick.AddListener(OnButtonClick);
        }

        SetSelected(false);
    }

    private void OnButtonClick()
    {
        onClickCallback?.Invoke(VariantKey, this);
    }

    public void SetSelected(bool selected)
    {
        if (Button_BG != null)
            Button_BG.color = selected
                ? SelectedColor
                : NormalColor;
    }
}