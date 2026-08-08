using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClubPoker.Networking.Models;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Purchasing;

public class DiamondIAPManager : MonoBehaviour
{
    public static DiamondIAPManager Instance { get; private set; }

    [Header("Purchase Mode")]
    public bool EnablePurchaseBypass = true;

    [Header("Editor Test Platform")]
    public TestPlatform EditorPlatform = TestPlatform.IOS;

    private StoreController storeController;
    private CatalogProvider catalogProvider;

    private bool isConnecting;
    private bool isStoreConnected;
    private bool isProductsLoaded;
    private bool isPurchasing;

    private PendingOrder pendingRealOrder;

    private readonly Dictionary<string, ShopPackageData> shopPackages = new Dictionary<string, ShopPackageData>();

    public bool IsPurchasing => isPurchasing;
    public bool IsIAPReady => isStoreConnected && isProductsLoaded;

    public event Action<DiamondPurchaseValidateRequest> PurchaseCreated;
    public event Action<string> PurchaseFailed;
    public event Action IAPInitialized;

    public enum TestPlatform
    {
        IOS,
        ANDROID
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetPackages(List<ShopPackageData> packages)
    {
        shopPackages.Clear();

        if (packages == null || packages.Count == 0)
        {
            Debug.LogWarning("[IAP] No shop packages received");
            return;
        }

        foreach (ShopPackageData package in packages)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.Id)) continue;
            shopPackages[package.Id] = package;
        }

        Debug.Log("[IAP] Server packages received: " + shopPackages.Count);

        if (!EnablePurchaseBypass) InitializeRealIAP();
    }

    public void BuyPackage(ShopPackageData package)
    {
        if (package == null)
        {
            FailPurchase("Package data missing");
            return;
        }

        BuyProduct(package.Id);
    }

    public void BuyProduct(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            FailPurchase("Package ID missing");
            return;
        }

        if (isPurchasing)
        {
            PurchaseFailed?.Invoke("Purchase already in progress");
            return;
        }

        if (EnablePurchaseBypass)
        {
            SimulatePurchase(packageId);
            return;
        }

        BuyRealProduct(packageId);
    }

    public async void InitializeRealIAP()
    {
        if (EnablePurchaseBypass) return;
        if (isConnecting || IsIAPReady) return;

        if (shopPackages.Count == 0)
        {
            Debug.LogWarning("[IAP] Cannot initialize because server packages are empty");
            return;
        }

        isConnecting = true;

        try
        {
            SetupStoreController();
            CreateCatalog();

            Debug.Log("[IAP] Connecting to store...");

            await storeController.Connect();

            isStoreConnected = true;

            Debug.Log("[IAP] Store connected");

            catalogProvider.FetchProducts(products =>
            {
                if (storeController == null) return;

                Debug.Log("[IAP] Fetching products from store...");
                storeController.FetchProducts(products);
            });
        }
        catch (Exception e)
        {
            isConnecting = false;
            isStoreConnected = false;
            isProductsLoaded = false;

            Debug.LogError("[IAP] Initialization exception: " + e);
            PurchaseFailed?.Invoke("IAP initialization failed");
        }
    }

    private void SetupStoreController()
    {
        if (storeController != null) return;

        storeController = UnityIAPServices.StoreController();

        storeController.OnStoreDisconnected += OnStoreDisconnected;
        storeController.OnProductsFetched += OnProductsFetched;
        storeController.OnProductsFetchFailed += OnProductsFetchFailed;
        storeController.OnPurchasesFetched += OnPurchasesFetched;
        storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
        storeController.OnPurchasePending += OnPurchasePending;
        storeController.OnPurchaseFailed += OnRealPurchaseFailed;
        storeController.OnPurchaseDeferred += OnPurchaseDeferred;
    }

    private void CreateCatalog()
    {
        catalogProvider = new CatalogProvider();

        foreach (KeyValuePair<string, ShopPackageData> item in shopPackages)
        {
            catalogProvider.AddProduct(item.Key, ProductType.Consumable);
            Debug.Log("[IAP] Product registered: " + item.Key);
        }
    }

    private void OnProductsFetched(List<Product> products)
    {
        isConnecting = false;
        isProductsLoaded = products != null && products.Count > 0;

        Debug.Log("[IAP] Products fetched: " + (products?.Count ?? 0));

        if (products != null)
        {
            foreach (Product product in products)
            {
                if (product == null) continue;

                Debug.Log(
                    "[IAP] Product | ID: " + product.definition.id +
                    " | Available: " + product.availableToPurchase +
                    " | Price: " + product.metadata.localizedPriceString
                );
            }
        }

        if (!isProductsLoaded)
        {
            PurchaseFailed?.Invoke("No IAP products available");
            return;
        }

        storeController.FetchPurchases();
        IAPInitialized?.Invoke();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        isConnecting = false;
        isProductsLoaded = false;

        Debug.LogError("[IAP] Product fetch failed: " + failure);
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        isConnecting = false;
        isStoreConnected = false;
        isProductsLoaded = false;

        Debug.LogError("[IAP] Store disconnected: " + failure);
    }

    private void OnPurchasesFetched(Orders orders)
    {
        Debug.Log("[IAP] Previous purchases fetched");

        if (orders == null) return;

        if (orders.PendingOrders != null)
        {
            foreach (PendingOrder order in orders.PendingOrders)
            {
                if (order != null) ProcessPendingOrder(order);
            }
        }
    }

    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        Debug.LogError("[IAP] Purchases fetch failed: " + failure);
    }

    private void BuyRealProduct(string packageId)
    {
        if (!IsIAPReady || storeController == null)
        {
            Debug.LogWarning("[IAP] IAP not ready. Initializing...");

            InitializeRealIAP();
            PurchaseFailed?.Invoke("IAP is not ready. Please try again.");
            return;
        }

        Product product = storeController.GetProducts().FirstOrDefault(x => x != null && x.definition.id == packageId);

        if (product == null)
        {
            FailPurchase("Product not found: " + packageId);
            return;
        }

        if (!product.availableToPurchase)
        {
            FailPurchase("Product unavailable: " + packageId);
            return;
        }

        isPurchasing = true;

        Debug.Log(
            "[IAP] Starting purchase | " +
            "Product: " + packageId +
            " | Price: " + product.metadata.localizedPriceString
        );

        storeController.PurchaseProduct(packageId);
    }

    private void OnPurchasePending(PendingOrder order)
    {
        ProcessPendingOrder(order);
    }

    private void ProcessPendingOrder(PendingOrder order)
    {
        if (order == null)
        {
            FailPurchase("Purchase order missing");
            return;
        }

        Product product = order.CartOrdered.Items().FirstOrDefault()?.Product;

        if (product == null)
        {
            FailPurchase("Purchased product missing");
            return;
        }

        string packageId = product.definition.id;
        string transactionId = order.Info != null ? order.Info.TransactionID : "";
        string receipt = GetReceipt(order);

        if (string.IsNullOrEmpty(packageId))
        {
            FailPurchase("Purchased package ID missing");
            return;
        }

        if (string.IsNullOrEmpty(transactionId))
        {
            FailPurchase("Transaction ID missing");
            return;
        }

        if (string.IsNullOrEmpty(receipt))
        {
            FailPurchase("Receipt missing");
            return;
        }

        pendingRealOrder = order;
        isPurchasing = true;

        DiamondPurchaseValidateRequest request = new DiamondPurchaseValidateRequest
        {
            PackageId = packageId,
            Platform = GetPlatform(),
            ReceiptId = transactionId,
            Receipt = receipt
        };

        Debug.Log(
            "[IAP] Purchase pending | " +
            "Package: " + packageId +
            " | Transaction: " + transactionId +
            " | Platform: " + request.Platform
        );

        PurchaseCreated?.Invoke(request);
    }

    private string GetReceipt(PendingOrder order)
    {
        if (order == null || order.Info == null) return "";

        try
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (order.Info.Apple != null && !string.IsNullOrEmpty(order.Info.Apple.jwsRepresentation))
                return order.Info.Apple.jwsRepresentation;
#endif

            if (!string.IsNullOrEmpty(order.Info.Receipt))
                return order.Info.Receipt;

            return JsonConvert.SerializeObject(order.Info);
        }
        catch (Exception e)
        {
            Debug.LogError("[IAP] Receipt read failed: " + e.Message);
            return "";
        }
    }

    private void OnRealPurchaseFailed(FailedOrder order)
    {
        pendingRealOrder = null;
        isPurchasing = false;

        string message = order != null ? order.FailureReason.ToString() : "Purchase failed";

        Debug.LogError("[IAP] Purchase failed: " + message);
        PurchaseFailed?.Invoke(message);
    }

    private void OnPurchaseDeferred(DeferredOrder order)
    {
        isPurchasing = false;

        Debug.LogWarning("[IAP] Purchase deferred");
        PurchaseFailed?.Invoke("Purchase is waiting for approval");
    }

    private void SimulatePurchase(string packageId)
    {
        isPurchasing = true;

        string receiptId = "TEST-" + Guid.NewGuid().ToString("N");
        string platform = GetPlatform();

        FakeDiamondReceipt fakeReceipt = new FakeDiamondReceipt
        {
            PackageId = packageId,
            ReceiptId = receiptId,
            Platform = platform,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        DiamondPurchaseValidateRequest request = new DiamondPurchaseValidateRequest
        {
            PackageId = packageId,
            Platform = platform,
            ReceiptId = receiptId,
            Receipt = JsonConvert.SerializeObject(fakeReceipt)
        };

        Debug.Log(
            "[IAP BYPASS] Purchase created | " +
            "Package: " + packageId +
            " | Receipt ID: " + receiptId +
            " | Platform: " + platform
        );

        PurchaseCreated?.Invoke(request);
    }

    public void CompletePurchase()
    {
        if (!EnablePurchaseBypass && pendingRealOrder != null && storeController != null)
        {
            storeController.ConfirmPurchase(pendingRealOrder);
            Debug.Log("[IAP] Real purchase confirmed with store");
        }

        pendingRealOrder = null;
        isPurchasing = false;

        Debug.Log("[IAP] Purchase completed");
    }

    public void FailPurchase(string message)
    {
        isPurchasing = false;

        string finalMessage = string.IsNullOrEmpty(message) ? "Purchase failed" : message;

        Debug.LogError("[IAP] " + finalMessage);
        PurchaseFailed?.Invoke(finalMessage);
    }

    public void CancelPurchase()
    {
        isPurchasing = false;
        PurchaseFailed?.Invoke("Purchase cancelled");
    }

    public string GetLocalizedPrice(string packageId)
    {
        if (EnablePurchaseBypass || !IsIAPReady || storeController == null) return "";

        Product product = storeController.GetProducts().FirstOrDefault(x => x != null && x.definition.id == packageId);

        return product?.metadata?.localizedPriceString ?? "";
    }

    private string GetPlatform()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return "ANDROID";
#elif UNITY_IOS && !UNITY_EDITOR
        return "IOS";
#else
        return EditorPlatform == TestPlatform.ANDROID ? "ANDROID" : "IOS";
#endif
    }

    private void OnDestroy()
    {
        if (storeController != null)
        {
            storeController.OnStoreDisconnected -= OnStoreDisconnected;
            storeController.OnProductsFetched -= OnProductsFetched;
            storeController.OnProductsFetchFailed -= OnProductsFetchFailed;
            storeController.OnPurchasesFetched -= OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
            storeController.OnPurchasePending -= OnPurchasePending;
            storeController.OnPurchaseFailed -= OnRealPurchaseFailed;
            storeController.OnPurchaseDeferred -= OnPurchaseDeferred;
        }

        if (Instance == this) Instance = null;
    }
}