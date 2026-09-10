using ClubPoker.Networking.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

public class GoldPrefab : MonoBehaviour
{
    public Button GoldBuyButton;
    public Text RequiredDiamondText;
    public Text GoldText;
    public Image IconImage;          // pack art, picked by size from ShopIconSO

    private GoldData goldData;
    private Action<GoldData> buyCallback;

    public void SetData(GoldData data, Action<GoldData> callback, Sprite icon = null)
    {
        goldData = data;
        buyCallback = callback;

        if (data == null) return;

        if (RequiredDiamondText != null) RequiredDiamondText.text = FormatNumber(data.Daimond);
        if (GoldText != null) GoldText.text = FormatNumber(data.Gold);

        // No icon configured for this tier — leave whatever the prefab ships with.
        if (IconImage != null && icon != null) IconImage.sprite = icon;

        if (GoldBuyButton != null)
        {
            GoldBuyButton.onClick.RemoveAllListeners();
            GoldBuyButton.onClick.AddListener(GoldBuyButtonOnTap);
        }
    }

    public void SetButtonInteractable(bool interactable)
    {
        if (GoldBuyButton != null) GoldBuyButton.interactable = interactable;
    }

    private void GoldBuyButtonOnTap()
    {
        if (goldData == null) return;
        buyCallback?.Invoke(goldData);
    }

    private string FormatNumber(int value)
    {
        if (value >= 1000000000) return (value / 1000000000f).ToString("0.#") + "B";
        if (value >= 1000000) return (value / 1000000f).ToString("0.#") + "M";
        if (value >= 1000) return (value / 1000f).ToString("0.#") + "K";
        return value.ToString();
    }

    private void OnDestroy()
    {
        if (GoldBuyButton != null) GoldBuyButton.onClick.RemoveListener(GoldBuyButtonOnTap);
    }
}