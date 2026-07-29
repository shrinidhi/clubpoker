using System;
using UnityEngine;
using UnityEngine.UI;

public class VariantPrefabScript : MonoBehaviour
{
    public Button VariantButton;
    public Text VariantText;

    private string variantValue;
    private Action<string, string> selectCallback;

    public void SetData(string displayName, string value, Action<string, string> callback)
    {
        variantValue = value;
        selectCallback = callback;

        if (VariantText != null)
            VariantText.text = displayName;

        if (VariantButton != null)
        {
            VariantButton.onClick.RemoveAllListeners();
            VariantButton.onClick.AddListener(OnVariantButtonTap);
        }
    }

    private void OnVariantButtonTap()
    {
        selectCallback?.Invoke(VariantText != null ? VariantText.text : variantValue, variantValue);
    }

    private void OnDestroy()
    {
        if (VariantButton != null)
            VariantButton.onClick.RemoveAllListeners();
    }
}