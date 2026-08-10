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

    private ShopPackageData packageData;
    private Action<ShopPackageData> buyCallback;

    public void SetData(
        ShopPackageData data,
        Action<ShopPackageData> callback)
    {
        packageData = data;
        buyCallback = callback;

        if (data == null)
            return;

        if (PriceText != null)
            PriceText.text = data.PriceLabel ?? "";

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