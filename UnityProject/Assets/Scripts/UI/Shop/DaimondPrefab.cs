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

    public void SetData(ShopPackageData data, Action<ShopPackageData> callback)
    {
        packageData = data;
        buyCallback = callback;

        if (data == null)
            return;

        if (PriceText != null)
            PriceText.text = data.PriceLabel;

        if (TotalDaimondText != null)
            TotalDaimondText.text = data.TotalDiamonds.ToString();

        if (DaimondText != null)
        {
            if(data.Diamonds== data.TotalDiamonds)
            {
                DaimondText.gameObject.SetActive(false);
            }
            else
            {
                DaimondText.gameObject.SetActive(true);
            }
            
            DaimondText.text = data.Diamonds.ToString();

            Canvas.ForceUpdateCanvases();

            RectTransform rect = DaimondText.rectTransform;

            rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                DaimondText.preferredWidth
            );
        }

        if (BuyButton != null)
        {
            BuyButton.onClick.RemoveAllListeners();
            BuyButton.onClick.AddListener(BuyButtonOnTap);
        }
    }

    private void BuyButtonOnTap()
    {
        if (packageData == null)
            return;

        buyCallback?.Invoke(packageData);
    }
}