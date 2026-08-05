using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ShopScreenScript : MonoBehaviour
{
    public Button BackButton;
    public Text CoinCountText;
    public Text DaimondCountText;

    public Button GoldButton;
    public Button DaimondButton;
    public Button ItemButton;

    public GameObject GoldPanel;
    public GameObject DaimondPanel;
    public GameObject ItemPanel;

    public Sprite SelectButtonSprite;
    public Sprite UnSelectButtonSprite;

    [Header("Diamond Packages")]
    public Transform DaimondContent;
    public GameObject DaimondPrefab;

    private bool isLoadingBalance;
    private bool isLoadingPackages;

    public RectTransform MidPanelRect;
    public GameObject ItemBanner;
    public GameObject DaimondBanner;

    private void Start()
    {
        if (BackButton != null)
        {
            BackButton.onClick.RemoveListener(BackButtonOnTap);
            BackButton.onClick.AddListener(BackButtonOnTap);
        }

        if (GoldButton != null)
        {
            GoldButton.onClick.RemoveListener(GoldButtonOnTap);
            GoldButton.onClick.AddListener(GoldButtonOnTap);
        }

        if (DaimondButton != null)
        {
            DaimondButton.onClick.RemoveListener(DaimondButtonOnTap);
            DaimondButton.onClick.AddListener(DaimondButtonOnTap);
        }

        if (ItemButton != null)
        {
            ItemButton.onClick.RemoveListener(ItemButtonOnTap);
            ItemButton.onClick.AddListener(ItemButtonOnTap);
        }
    }

    private void OnEnable()
    {
        GoldButtonOnTap();
        LoadShopBalance().Forget();
        LoadDiamondPackages().Forget();
    }

    private void OnDestroy()
    {
        if (BackButton != null)
            BackButton.onClick.RemoveListener(BackButtonOnTap);

        if (GoldButton != null)
            GoldButton.onClick.RemoveListener(GoldButtonOnTap);

        if (DaimondButton != null)
            DaimondButton.onClick.RemoveListener(DaimondButtonOnTap);

        if (ItemButton != null)
            ItemButton.onClick.RemoveListener(ItemButtonOnTap);
    }

    private async UniTaskVoid LoadShopBalance()
    {
        if (isLoadingBalance ||
            AuthManager.Instance == null)
            return;

        isLoadingBalance = true;

        try
        {
            if (DaimondCountText != null)
                DaimondCountText.text = "...";

            DiamondData diamondData =
                await AuthManager.Instance
                    .GetDiamondsAsync();

            if (this == null)
                return;

            if (DaimondCountText != null)
            {
                DaimondCountText.text =
                    diamondData != null
                        ? FormatNumber(diamondData.Balance)
                        : "0";
            }

            ChipsData chipsData =
                await AuthManager.Instance
                    .GetChipsAsync();

            if (this == null)
                return;

            if (CoinCountText != null)
            {
                CoinCountText.text =
                    chipsData != null
                        ? FormatNumber(
                            chipsData.AvailableChips
                        )
                        : "0";
            }
        }
        finally
        {
            isLoadingBalance = false;
        }
    }

    private async UniTaskVoid LoadDiamondPackages()
    {
        if (isLoadingPackages ||
            AuthManager.Instance == null)
            return;

        isLoadingPackages = true;

        try
        {
            List<ShopPackageData> packages =
                await AuthManager.Instance
                    .GetShopPackagesAsync();

            if (this == null)
                return;

            ClearDiamondPackages();

            if (packages == null ||
                packages.Count == 0)
            {
                Debug.LogWarning(
                    "[Shop] No diamond packages found"
                );

                return;
            }

            foreach (ShopPackageData package in packages)
            {
                GameObject obj =
                    Instantiate(
                        DaimondPrefab,
                        DaimondContent
                    );

                DaimondPrefab prefab =
                    obj.GetComponent<DaimondPrefab>();

                if (prefab != null)
                {
                    prefab.SetData(
                        package,
                        BuyDiamondPackage
                    );
                }
                else
                {
                    Destroy(obj);
                }
            }

            Debug.Log(
                "[Shop] Diamond packages generated: " +
                packages.Count
            );
        }
        finally
        {
            isLoadingPackages = false;
        }
    }

    private void BuyDiamondPackage(
        ShopPackageData package)
    {
        if (package == null)
            return;

        Debug.Log(
            $"Buy diamond package | " +
            $"ID: {package.Id} | " +
            $"Diamonds: {package.TotalDiamonds} | " +
            $"Price: {package.PriceLabel}"
        );

    }

    private void ClearDiamondPackages()
    {
        if (DaimondContent == null)
            return;

        for (int i = DaimondContent.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                DaimondContent.GetChild(i).gameObject
            );
        }
    }

    private void BackButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    private void GoldButtonOnTap()
    {
        SetTab(GoldButton, GoldPanel);
    }

    private void DaimondButtonOnTap()
    {
        SetTab(DaimondButton, DaimondPanel);
    }

    private void ItemButtonOnTap()
    {
        SetTab(ItemButton, ItemPanel);
    }

    private void SetTab(Button selectedButton, GameObject selectedPanel)
    {
        if (GoldButton != null && GoldButton.image != null)
            GoldButton.image.sprite = selectedButton == GoldButton ? SelectButtonSprite : UnSelectButtonSprite;

        if (DaimondButton != null && DaimondButton.image != null)
            DaimondButton.image.sprite = selectedButton == DaimondButton ? SelectButtonSprite : UnSelectButtonSprite;

        if (ItemButton != null && ItemButton.image != null)
            ItemButton.image.sprite = selectedButton == ItemButton ? SelectButtonSprite : UnSelectButtonSprite;

        if (GoldPanel != null)
            GoldPanel.SetActive(selectedPanel == GoldPanel);

        if (DaimondPanel != null)
            DaimondPanel.SetActive(selectedPanel == DaimondPanel);

        if (ItemPanel != null)
            ItemPanel.SetActive(selectedPanel == ItemPanel);

        if (selectedPanel == GoldPanel && MidPanelRect != null)
        {
            SetTop(MidPanelRect, 389);
            ItemBanner.SetActive(false);
            DaimondBanner.SetActive(false);
        }
        else if (selectedPanel == DaimondPanel && MidPanelRect != null)
        {
            SetTop(MidPanelRect, 533);
            ItemBanner.SetActive(false);
            DaimondBanner.SetActive(true);
        }
        else if (selectedPanel == ItemPanel && MidPanelRect != null)
        {
            SetTop(MidPanelRect, 610);
            ItemBanner.SetActive(true);
            DaimondBanner.SetActive(false);
        }
            
    }

    private void SetTop(RectTransform rect, float top)
    {
        Vector2 offsetMax = rect.offsetMax;
        offsetMax.y = -top;
        rect.offsetMax = offsetMax;
    }

    private string FormatNumber(int value)
    {
        if (value >= 1000000000)
            return (value / 1000000000f)
                .ToString("0.#") + "B";

        if (value >= 1000000)
            return (value / 1000000f)
                .ToString("0.#") + "M";

        if (value >= 1000)
            return (value / 1000f)
                .ToString("0.#") + "K";

        return value.ToString();
    }
}