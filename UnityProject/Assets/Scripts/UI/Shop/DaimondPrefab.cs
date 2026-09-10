using System;
using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

public class DaimondPrefab : MonoBehaviour
{
    public Button BuyButton;
    public Text PriceText;
    public Text TotalDaimondText;
    public Text DaimondText;
    public GameObject CutLine;
    public Image IconImage;          // pack art, picked by size from ShopIconSO

    private ShopPackageData packageData;
    private Action<ShopPackageData> buyCallback;

    public void SetData(
        ShopPackageData data,
        Action<ShopPackageData> callback,
        Sprite icon = null)
    {
        packageData = data;
        buyCallback = callback;

        if (data == null)
            return;

        // No icon configured for this tier — leave whatever the prefab ships with.
        if (IconImage != null && icon != null)
            IconImage.sprite = icon;

        RefreshPrice();

        if (TotalDaimondText != null)
            TotalDaimondText.text =
                data.TotalDiamonds.ToString();

        bool hasBonus =
            data.BonusDiamonds > 0 &&
            data.Diamonds != data.TotalDiamonds;

        if (DaimondText != null)
        {
            DaimondText.gameObject.SetActive(hasBonus);
            DaimondText.text = data.Diamonds.ToString();

            if (hasBonus)
            {
                Canvas.ForceUpdateCanvases();

                RectTransform rect =
                    DaimondText.rectTransform;

                rect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    DaimondText.preferredWidth
                );
            }
        }

        if (CutLine != null)
            CutLine.SetActive(hasBonus);

        SetButtonInteractable(true);

        if (BuyButton != null)
        {
            BuyButton.onClick.RemoveAllListeners();
            BuyButton.onClick.AddListener(BuyButtonOnTap);
        }
    }

    /// <summary>
    /// Price shown is the store's own localized string (₹99.00, $1.99) whenever the
    /// store has answered — both Google and Apple require their price, in their
    /// currency, and the server's priceLabel is one fixed currency.
    ///
    /// The server label is the fallback: the tiles are built before the store
    /// finishes fetching, and bypass builds never talk to a store at all. Callable
    /// again later, which is what the shop does once IAP reports ready.
    /// </summary>
    public void RefreshPrice()
    {
        if (PriceText == null || packageData == null)
            return;

        string storePrice = DiamondIAPManager.Instance != null
            ? DiamondIAPManager.Instance.GetLocalizedPrice(packageData.Id)
            : "";

        PriceText.text = !string.IsNullOrEmpty(storePrice)
            ? storePrice
            : packageData.PriceLabel ?? "";
    }

    public void SetButtonInteractable(bool interactable)
    {
        if (BuyButton != null)
            BuyButton.interactable = interactable;
    }

    private void BuyButtonOnTap()
    {
        if (packageData == null)
            return;

        buyCallback?.Invoke(packageData);
    }

    private void OnDestroy()
    {
        if (BuyButton != null)
            BuyButton.onClick.RemoveListener(BuyButtonOnTap);
    }
}