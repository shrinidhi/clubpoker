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

    public void SetInteractable(bool interactable)
    {
        if (VariantButton == null)
        {
            Debug.LogError("VariantButton is not assigned on " + gameObject.name);
            return;
        }

        VariantButton.interactable = interactable;

        Debug.Log(
            "Variant: " +
            (VariantText != null ? VariantText.text : variantValue) +
            " | Interactable: " +
            VariantButton.interactable
        );
    }

    private void OnVariantButtonTap()
    {
        if (VariantButton != null && !VariantButton.interactable)
            return;

        selectCallback?.Invoke(
            VariantText != null ? VariantText.text : variantValue,
            variantValue
        );
    }

    private void OnDestroy()
    {
        if (VariantButton != null)
            VariantButton.onClick.RemoveAllListeners();
    }
}