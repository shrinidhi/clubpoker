using ClubPoker.Networking.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

public class Club_VariantPrefabScreen : MonoBehaviour
{
    public Button Variant_Button;
    public GameObject Lock_BG;
    public Image Variant_Image;
    private ClubTableVariantData currentVariant;
    private Action<ClubTableVariantData> onClickCallback;

    public void SetData(
        ClubTableVariantData variantData, Sprite variantSprite,
        Action<ClubTableVariantData> callback)
    {
        currentVariant = variantData;
        onClickCallback = callback;

        if (Variant_Image != null)
            Variant_Image.sprite = variantSprite;
        if (Lock_BG != null)
            Lock_BG.SetActive(variantData.IsLocked);

        if (Variant_Button != null)
        {
            Variant_Button.interactable =
                !variantData.IsLocked;

            Variant_Button.onClick.RemoveAllListeners();
            Variant_Button.onClick.AddListener(
                OnVariantButtonClick);
        }
    }

    private void OnVariantButtonClick()
    {
        if (currentVariant == null ||
            currentVariant.IsLocked)
            return;

        onClickCallback?.Invoke(currentVariant);
    }
}