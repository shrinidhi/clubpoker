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

    public string variantname;
    private Action<string, FilterTableByVariantPrefabScrtipt> onClickCallback;

    public void SetData(
        string displayName,
        Action<string, FilterTableByVariantPrefabScrtipt> callback)
    {
        variantname = displayName;
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
        onClickCallback?.Invoke(variantname, this);
    }

    public void SetSelected(bool selected)
    {
        if (Button_BG != null)
            Button_BG.color = selected ? SelectedColor : NormalColor;
    }
}