using System;
using System.Collections.Generic;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ShopScreenScript : MonoBehaviour
{
    [Header("Top UI")]
    public Button BackButton;
    public Text CoinCountText;
    public Text DaimondCountText;
    public Text MainMenuDaimondCountText;

    [Header("Tabs")]
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

    [Header("Gold Packages")]
    public Transform GoldContent;
    public GameObject GoldPrefab;
    public List<GoldData> GoldList = new List<GoldData>();

    [Header("Middle Panel")]
    public RectTransform MidPanelRect;
    public GameObject ItemBanner;
    public GameObject DaimondBanner;

    [Header("Purchase Status")]
    public Text PurchaseStatusText;
    public GameObject PurchaseLoadingPanel;

    [Header("Purchase Success Popup")]
    public GameObject PurchaseSuccessfullScreen;
    public RectTransform PurchaseSuccessfullPopup;
    public Text PurchaseSuccessMsg;

    [Header("Purchase Success Animation")]
    public float SuccessPopupEnterDuration = 0.5f;
    public float SuccessPopupStayDuration = 2f;
    public float SuccessPopupExitDuration = 0.5f;
    public float SuccessPopupBottomOffset = 900f;
    public float SuccessPopupTopOffset = 900f;

    private bool isLoadingBalance;
    private bool isLoadingPackages;
    private bool isValidatingPurchase;
    private bool isSuccessPopupPlaying;
    private bool isExchangingGold;

    private int currentDiamondBalance;
    private Vector2 purchasePopupCenterPosition;

    private readonly List<DaimondPrefab> generatedDiamondPrefabs = new List<DaimondPrefab>();
    private readonly List<GoldPrefab> generatedGoldPrefabs = new List<GoldPrefab>();

    private void Start()
    {
        CreateDefaultGoldList();
        GenerateGoldPackages();

        if (PurchaseSuccessfullPopup != null) purchasePopupCenterPosition = PurchaseSuccessfullPopup.anchoredPosition;
        if (PurchaseSuccessfullScreen != null) PurchaseSuccessfullScreen.SetActive(false);

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

        RegisterPurchaseEvents();
        ClearPurchaseStatus();
        SetPurchaseLoading(false);
    }

    private void OnEnable()
    {
        RegisterPurchaseEvents();
        GoldButtonOnTap();
        LoadShopBalance().Forget();
        LoadDiamondPackages().Forget();
    }

    private void OnDisable()
    {
        UnregisterPurchaseEvents();

        if (PurchaseSuccessfullScreen != null) PurchaseSuccessfullScreen.SetActive(false);
        if (PurchaseSuccessfullPopup != null) PurchaseSuccessfullPopup.anchoredPosition = purchasePopupCenterPosition;

        isSuccessPopupPlaying = false;
    }

    private void OnDestroy()
    {
        if (BackButton != null) BackButton.onClick.RemoveListener(BackButtonOnTap);
        if (GoldButton != null) GoldButton.onClick.RemoveListener(GoldButtonOnTap);
        if (DaimondButton != null) DaimondButton.onClick.RemoveListener(DaimondButtonOnTap);
        if (ItemButton != null) ItemButton.onClick.RemoveListener(ItemButtonOnTap);

        UnregisterPurchaseEvents();
    }

    private void CreateDefaultGoldList()
    {
        GoldList.Clear();
        GoldList.Add(new GoldData { Daimond = 100, Gold = 1000 });
        GoldList.Add(new GoldData { Daimond = 500, Gold = 5500 });
        GoldList.Add(new GoldData { Daimond = 1000, Gold = 12000 });
        GoldList.Add(new GoldData { Daimond = 5000, Gold = 65000 });
       
    }

    private void GenerateGoldPackages()
    {
        ClearGoldPackages();

        if (GoldContent == null || GoldPrefab == null)
        {
            Debug.LogError("[Shop] GoldContent or GoldPrefab missing");
            return;
        }

        foreach (GoldData data in GoldList)
        {
            if (data == null) continue;

            GameObject obj = Instantiate(GoldPrefab, GoldContent);
            GoldPrefab prefab = obj.GetComponent<GoldPrefab>();

            if (prefab == null)
            {
                Destroy(obj);
                continue;
            }

            prefab.SetData(data, BuyGoldPackage);
            generatedGoldPrefabs.Add(prefab);
        }
    }

    private void ClearGoldPackages()
    {
        generatedGoldPrefabs.Clear();

        if (GoldContent == null) return;

        for (int i = GoldContent.childCount - 1; i >= 0; i--) Destroy(GoldContent.GetChild(i).gameObject);
    }

    private void RegisterPurchaseEvents()
    {
        if (DiamondIAPManager.Instance == null) return;

        DiamondIAPManager.Instance.PurchaseCreated -= OnPurchaseCreated;
        DiamondIAPManager.Instance.PurchaseCreated += OnPurchaseCreated;
        DiamondIAPManager.Instance.PurchaseFailed -= OnPurchaseFailed;
        DiamondIAPManager.Instance.PurchaseFailed += OnPurchaseFailed;
    }

    private void UnregisterPurchaseEvents()
    {
        if (DiamondIAPManager.Instance == null) return;

        DiamondIAPManager.Instance.PurchaseCreated -= OnPurchaseCreated;
        DiamondIAPManager.Instance.PurchaseFailed -= OnPurchaseFailed;
    }

    private async UniTaskVoid LoadShopBalance()
    {
        if (isLoadingBalance || AuthManager.Instance == null) return;

        isLoadingBalance = true;

        try
        {
            if (DaimondCountText != null) DaimondCountText.text = "...";

            DiamondData diamondData = await AuthManager.Instance.GetDiamondsAsync();

            if (this == null) return;

            currentDiamondBalance = diamondData != null ? diamondData.Available : 0;

            if (DaimondCountText != null) DaimondCountText.text = diamondData != null ? FormatNumber(diamondData.Balance) : "0";
            if (MainMenuDaimondCountText != null) MainMenuDaimondCountText.text = diamondData != null ? FormatNumber(diamondData.Balance) : "0";

            ChipsData chipsData = await AuthManager.Instance.GetChipsAsync();

            if (this == null) return;

            if (CoinCountText != null) CoinCountText.text = chipsData != null ? FormatNumber(chipsData.AvailableChips) : "0";
        }
        catch (Exception e)
        {
            Debug.LogError("[Shop] Balance load failed: " + e.Message);

            currentDiamondBalance = 0;

            if (DaimondCountText != null) DaimondCountText.text = "0";
            if (MainMenuDaimondCountText != null) MainMenuDaimondCountText.text = "0";
            if (CoinCountText != null) CoinCountText.text = "0";
        }
        finally
        {
            isLoadingBalance = false;
        }
    }

    private async UniTaskVoid LoadDiamondPackages()
    {
        if (isLoadingPackages || AuthManager.Instance == null) return;

        isLoadingPackages = true;

        try
        {
            List<ShopPackageData> packages = await AuthManager.Instance.GetShopPackagesAsync();

            if (this == null) return;

            if (DiamondIAPManager.Instance != null)
                DiamondIAPManager.Instance.SetPackages(packages);

            ClearDiamondPackages();

            if (packages == null || packages.Count == 0)
            {
                Debug.LogWarning("[Shop] No diamond packages found");
                return;
            }

            foreach (ShopPackageData package in packages)
            {
                if (package == null) continue;

                GameObject obj = Instantiate(DaimondPrefab, DaimondContent);
                DaimondPrefab prefab = obj.GetComponent<DaimondPrefab>();

                if (prefab == null)
                {
                    Destroy(obj);
                    continue;
                }

                prefab.SetData(package, BuyDiamondPackage);
                generatedDiamondPrefabs.Add(prefab);
            }

            Debug.Log("[Shop] Diamond packages generated: " + packages.Count);
        }
        catch (Exception e)
        {
            Debug.LogError("[Shop] Package load failed: " + e.Message);
        }
        finally
        {
            isLoadingPackages = false;
        }
    }

    private void BuyDiamondPackage(ShopPackageData package)
    {
        if (package == null) return;

        if (isValidatingPurchase)
        {
            ShowPurchaseStatus("Purchase already in progress");
            return;
        }

        if (DiamondIAPManager.Instance == null)
        {
            ShowPurchaseStatus("Purchase manager not available");
            return;
        }

        if (DiamondIAPManager.Instance.IsPurchasing)
        {
            ShowPurchaseStatus("Purchase already in progress");
            return;
        }

        Debug.Log("[Shop] Buy diamond package | ID: " + package.Id + " | Diamonds: " + package.TotalDiamonds + " | Price: " + package.PriceLabel);

        ClearPurchaseStatus();
        DiamondIAPManager.Instance.BuyPackage(package);
    }

    private void OnPurchaseCreated(DiamondPurchaseValidateRequest request)
    {
        ValidatePurchase(request).Forget();
    }

    private async UniTaskVoid ValidatePurchase(DiamondPurchaseValidateRequest request)
    {
        if (isValidatingPurchase) return;

        if (request == null)
        {
            DiamondIAPManager.Instance?.FailPurchase("Purchase request missing");
            return;
        }

        if (AuthManager.Instance == null)
        {
            DiamondIAPManager.Instance?.FailPurchase("AuthManager not available");
            return;
        }

        isValidatingPurchase = true;
        SetPurchaseLoading(true);
        SetPackageButtonsInteractable(false);
        ShowPurchaseStatus("Validating purchase...");

        try
        {
            DiamondPurchaseValidateData response = await AuthManager.Instance.ValidateDiamondPurchaseAsync(request);

            if (this == null) return;

            if (response == null || !response.Success)
            {
                DiamondIAPManager.Instance?.FailPurchase("Purchase validation failed");
                return;
            }

            DiamondIAPManager.Instance?.CompletePurchase();

            DiamondData diamondData = await AuthManager.Instance.GetDiamondsAsync();

            if (this == null) return;

            currentDiamondBalance = diamondData != null ? diamondData.Available : 0;

            if (DaimondCountText != null) DaimondCountText.text = diamondData != null ? FormatNumber(diamondData.Balance) : "0";
            if (MainMenuDaimondCountText != null) MainMenuDaimondCountText.text = diamondData != null ? FormatNumber(diamondData.Balance) : "0";

            ShowPurchaseStatus("+" + response.TotalDiamonds + " diamonds added");
            ShowPurchaseSuccessPopup(response).Forget();

            Debug.Log("[Shop] Purchase completed | Transaction: " + response.TransactionId + " | Package: " + response.PackageId + " | Base: " + response.DiamondsGranted + " | Bonus: " + response.BonusDiamonds + " | Total: " + response.TotalDiamonds + " | Platform: " + response.Platform + " | Balance: " + (diamondData != null ? diamondData.Balance : 0));

            ClearPurchaseStatusAfterDelay().Forget();
        }
        catch (Exception e)
        {
            Debug.LogError("[Shop] Purchase validation failed: " + e);
            DiamondIAPManager.Instance?.FailPurchase(string.IsNullOrEmpty(e.Message) ? "Purchase failed" : e.Message);
        }
        finally
        {
            isValidatingPurchase = false;
            SetPurchaseLoading(false);
            SetPackageButtonsInteractable(true);
        }
    }

    private void OnPurchaseFailed(string message)
    {
        isValidatingPurchase = false;
        SetPurchaseLoading(false);
        SetPackageButtonsInteractable(true);
        ShowPurchaseStatus(string.IsNullOrEmpty(message) ? "Purchase failed" : message);
        ClearPurchaseStatusAfterDelay().Forget();
    }

    private void SetPackageButtonsInteractable(bool interactable)
    {
        foreach (DaimondPrefab prefab in generatedDiamondPrefabs) if (prefab != null) prefab.SetButtonInteractable(interactable);
    }

    private async UniTaskVoid ClearPurchaseStatusAfterDelay()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: destroyCancellationToken);

        if (this != null) ClearPurchaseStatus();
    }

    private void SetPurchaseLoading(bool active)
    {
        if (PurchaseLoadingPanel != null) PurchaseLoadingPanel.SetActive(active);
    }

    private void ShowPurchaseStatus(string message)
    {
        if (PurchaseStatusText == null) return;

        PurchaseStatusText.gameObject.SetActive(true);
        PurchaseStatusText.text = message;
    }

    private void ClearPurchaseStatus()
    {
        if (PurchaseStatusText == null) return;

        PurchaseStatusText.text = "";
        PurchaseStatusText.gameObject.SetActive(false);
    }

    private async UniTaskVoid ShowPurchaseSuccessPopup(DiamondPurchaseValidateData response)
    {
        if (response == null || PurchaseSuccessfullScreen == null || PurchaseSuccessfullPopup == null || isSuccessPopupPlaying) return;

        isSuccessPopupPlaying = true;

        try
        {
            if (PurchaseSuccessMsg != null)
            {
                PurchaseSuccessMsg.text = response.BonusDiamonds > 0
                    ? "Purchase Successful!\n" + response.DiamondsGranted + " Diamonds + " + response.BonusDiamonds + " Bonus\nTotal " + response.TotalDiamonds + " Diamonds added successfully."
                    : "Purchase Successful!\n" + response.TotalDiamonds + " Diamonds added successfully.";
            }

            Vector2 bottomPosition = purchasePopupCenterPosition + Vector2.down * SuccessPopupBottomOffset;
            Vector2 topPosition = purchasePopupCenterPosition + Vector2.up * SuccessPopupTopOffset;

            PurchaseSuccessfullPopup.anchoredPosition = bottomPosition;
            PurchaseSuccessfullScreen.SetActive(true);

            await AnimatePurchasePopup(bottomPosition, purchasePopupCenterPosition, SuccessPopupEnterDuration);
            await UniTask.Delay(TimeSpan.FromSeconds(SuccessPopupStayDuration), ignoreTimeScale: true, cancellationToken: destroyCancellationToken);
            await AnimatePurchasePopup(purchasePopupCenterPosition, topPosition, SuccessPopupExitDuration);

            PurchaseSuccessfullScreen.SetActive(false);
            PurchaseSuccessfullPopup.anchoredPosition = purchasePopupCenterPosition;
        }
        catch (OperationCanceledException)
        {
            if (PurchaseSuccessfullScreen != null) PurchaseSuccessfullScreen.SetActive(false);
            if (PurchaseSuccessfullPopup != null) PurchaseSuccessfullPopup.anchoredPosition = purchasePopupCenterPosition;
        }
        finally
        {
            isSuccessPopupPlaying = false;
        }
    }

    private async UniTask AnimatePurchasePopup(Vector2 startPosition, Vector2 endPosition, float duration)
    {
        if (PurchaseSuccessfullPopup == null) return;

        if (duration <= 0f)
        {
            PurchaseSuccessfullPopup.anchoredPosition = endPosition;
            return;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            PurchaseSuccessfullPopup.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, smoothProgress);
            await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken);
        }

        PurchaseSuccessfullPopup.anchoredPosition = endPosition;
    }

    private void ClearDiamondPackages()
    {
        generatedDiamondPrefabs.Clear();

        if (DaimondContent == null) return;

        for (int i = DaimondContent.childCount - 1; i >= 0; i--) Destroy(DaimondContent.GetChild(i).gameObject);
    }

    private void BuyGoldPackage(GoldData data)
    {
        if (data == null || isExchangingGold) return;

        if (currentDiamondBalance < data.Daimond)
        {
            ShowPurchaseStatus("Not enough diamonds. Required: " + FormatNumber(data.Daimond) + ", Available: " + FormatNumber(currentDiamondBalance));
            ClearPurchaseStatusAfterDelay().Forget();
            return;
        }

        ExchangeGold(data).Forget();
    }

    private async UniTaskVoid ExchangeGold(GoldData data)
    {
        if (data == null || isExchangingGold || AuthManager.Instance == null) return;

        isExchangingGold = true;
        SetGoldButtonsInteractable(false);
        SetPurchaseLoading(true);
        ShowPurchaseStatus("Exchanging diamonds...");

        try
        {
            GoldExchangeData response = await AuthManager.Instance.ExchangeDiamondsToChipsAsync(data.Daimond, data.Gold);

            if (this == null) return;

            if (response == null || !response.Success)
            {
                ShowPurchaseStatus("Gold exchange failed");
                ClearPurchaseStatusAfterDelay().Forget();
                return;
            }

            currentDiamondBalance = response.NewDiamondBalance;

            if (DaimondCountText != null) DaimondCountText.text = FormatNumber(response.NewDiamondBalance);
            if (MainMenuDaimondCountText != null) MainMenuDaimondCountText.text = FormatNumber(response.NewDiamondBalance);
            if (CoinCountText != null) CoinCountText.text = FormatNumber(response.NewWalletChips);

            ShowPurchaseStatus(FormatNumber(response.ChipsReceived) + " chips received");
            ShowGoldExchangeSuccessPopup(response).Forget();

            Debug.Log("[Shop] Gold exchange completed | Diamonds spent: " + response.DiamondsSpent + " | Chips received: " + response.ChipsReceived + " | Diamond balance: " + response.NewDiamondBalance + " | Wallet chips: " + response.NewWalletChips);

            ClearPurchaseStatusAfterDelay().Forget();
        }
        catch (Exception e)
        {
            Debug.LogError("[Shop] Gold exchange failed: " + e);
            ShowPurchaseStatus(string.IsNullOrEmpty(e.Message) ? "Gold exchange failed" : e.Message);
            ClearPurchaseStatusAfterDelay().Forget();
        }
        finally
        {
            isExchangingGold = false;
            SetGoldButtonsInteractable(true);
            SetPurchaseLoading(false);
        }
    }

    private void SetGoldButtonsInteractable(bool interactable)
    {
        foreach (GoldPrefab prefab in generatedGoldPrefabs) if (prefab != null) prefab.SetButtonInteractable(interactable);
    }

    private async UniTaskVoid ShowGoldExchangeSuccessPopup(GoldExchangeData response)
    {
        if (response == null || PurchaseSuccessfullScreen == null || PurchaseSuccessfullPopup == null || isSuccessPopupPlaying) return;

        isSuccessPopupPlaying = true;

        try
        {
            if (PurchaseSuccessMsg != null) PurchaseSuccessMsg.text = "Exchange Successful!\n" + FormatNumber(response.DiamondsSpent) + " Diamonds exchanged for\n" + FormatNumber(response.ChipsReceived) + " Chips successfully.";

            Vector2 bottomPosition = purchasePopupCenterPosition + Vector2.down * SuccessPopupBottomOffset;
            Vector2 topPosition = purchasePopupCenterPosition + Vector2.up * SuccessPopupTopOffset;

            PurchaseSuccessfullPopup.anchoredPosition = bottomPosition;
            PurchaseSuccessfullScreen.SetActive(true);

            await AnimatePurchasePopup(bottomPosition, purchasePopupCenterPosition, SuccessPopupEnterDuration);
            await UniTask.Delay(TimeSpan.FromSeconds(SuccessPopupStayDuration), ignoreTimeScale: true, cancellationToken: destroyCancellationToken);
            await AnimatePurchasePopup(purchasePopupCenterPosition, topPosition, SuccessPopupExitDuration);

            PurchaseSuccessfullScreen.SetActive(false);
            PurchaseSuccessfullPopup.anchoredPosition = purchasePopupCenterPosition;
        }
        catch (OperationCanceledException)
        {
            if (PurchaseSuccessfullScreen != null) PurchaseSuccessfullScreen.SetActive(false);
            if (PurchaseSuccessfullPopup != null) PurchaseSuccessfullPopup.anchoredPosition = purchasePopupCenterPosition;
        }
        finally
        {
            isSuccessPopupPlaying = false;
        }
    }

    private void BackButtonOnTap()
    {
        if (isValidatingPurchase || isExchangingGold) return;
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
        if (GoldButton != null && GoldButton.image != null) GoldButton.image.sprite = selectedButton == GoldButton ? SelectButtonSprite : UnSelectButtonSprite;
        if (DaimondButton != null && DaimondButton.image != null) DaimondButton.image.sprite = selectedButton == DaimondButton ? SelectButtonSprite : UnSelectButtonSprite;
        if (ItemButton != null && ItemButton.image != null) ItemButton.image.sprite = selectedButton == ItemButton ? SelectButtonSprite : UnSelectButtonSprite;

        if (GoldPanel != null) GoldPanel.SetActive(selectedPanel == GoldPanel);
        if (DaimondPanel != null) DaimondPanel.SetActive(selectedPanel == DaimondPanel);
        if (ItemPanel != null) ItemPanel.SetActive(selectedPanel == ItemPanel);

        if (selectedPanel == GoldPanel)
        {
            if (MidPanelRect != null) SetTop(MidPanelRect, 389);
            if (ItemBanner != null) ItemBanner.SetActive(false);
            if (DaimondBanner != null) DaimondBanner.SetActive(false);
        }
        else if (selectedPanel == DaimondPanel)
        {
            if (MidPanelRect != null) SetTop(MidPanelRect, 533);
            if (ItemBanner != null) ItemBanner.SetActive(false);
            if (DaimondBanner != null) DaimondBanner.SetActive(true);
        }
        else
        {
            if (MidPanelRect != null) SetTop(MidPanelRect, 610);
            if (ItemBanner != null) ItemBanner.SetActive(true);
            if (DaimondBanner != null) DaimondBanner.SetActive(false);
        }
    }

    private void SetTop(RectTransform rect, float top)
    {
        if (rect == null) return;

        Vector2 offset = rect.offsetMax;
        offset.y = -top;
        rect.offsetMax = offset;
    }

    private string FormatNumber(int value)
    {
        if (value >= 1000000000) return (value / 1000000000f).ToString("0.#") + "B";
        if (value >= 1000000) return (value / 1000000f).ToString("0.#") + "M";
        if (value >= 1000) return (value / 1000f).ToString("0.#") + "K";
        return value.ToString();
    }
}