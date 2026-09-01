using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using ClubPoker.Core;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using ClubPoker.UI;
using Newtonsoft.Json;

namespace ClubPoker.Lobby
{
    public class LobbyController : MonoBehaviour
    {
        [Header("Table List")]
        [SerializeField] private Transform contentParent;
        [SerializeField] private GameObject tablePrefab;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private GameObject emptyStateLabel;

        [Header("Variant Prefab Filter")]
        [SerializeField] private Transform variantContentParent;
        [SerializeField] private GameObject variantPrefab;
        [SerializeField] private TextAsset LobbyVariantJson;

        [Header("Buy-in Popup")]
        [SerializeField] private BuyInView buyInView;

        [Header("Chips Balance")]
        [SerializeField] private TextMeshProUGUI chipsText;
        [SerializeField] private Text DaimondText;

        private readonly Dictionary<string, LobbyTableItemUI> _tableMap = new();
        private AsyncOperationHandle<SceneInstance> _preloadHandle;
        private bool _isPreloaded;
        private bool _isPolling;

        private string _currentVariant = "all";

        private LobbyVariantResponse lobbyVariantResponse;

        [Header("Variant Selection")]
        [SerializeField] private VariantSO VariantSO;
        [SerializeField] private GameObject Variant_SelectionPanel;
        [SerializeField] private GameObject  LobbyPanel;
        [SerializeField] private Button   LobbyPanel_BackButton;

        [Header("Botton Button")]
        [SerializeField] private Button Club_Button;
        [SerializeField] private Button Shop_Button;
        [SerializeField] private Button Mission_Button;
        [SerializeField] private Button MTT_Button;

        public GameObject ShopScreen;

        private void Start()
        {
            LobbyPanel_BackButton.onClick.AddListener(LobbyPanel_BackButtonOnTap);
            Club_Button.onClick.AddListener(Club_ButtonOnTap);
            Shop_Button.onClick.AddListener(Shop_ButtonOnTap);
            Mission_Button.onClick.AddListener(Mission_ButtonOnTap);
            MTT_Button.onClick.AddListener(MTT_ButtonOnTap);
            LoadVariantJson();
            GenerateVariantPrefabs();

            Club_Button.image.color = new Color32(255, 255, 255, 0);
            Shop_Button.image.color = new Color32(255, 255, 255, 0);
            Mission_Button.image.color = new Color32(255, 255, 255, 0);
            MTT_Button.image.color = new Color32(255, 255, 255, 0);
        }

        void Club_ButtonOnTap()
        {
            GameSceneManager.Instance.LoadScene("Scene_MainMenu");
            Club_Button.image.color = new Color32(255, 255, 255, 255);
            Shop_Button.image.color = new Color32(255, 255, 255, 0);
            Mission_Button.image.color = new Color32(255, 255, 255, 0);
            MTT_Button.image.color = new Color32(255, 255, 255, 0);
        }

        void Shop_ButtonOnTap()
        {
            Club_Button.image.color = new Color32(255, 255, 255, 0);
            //Shop_Button.image.color = new Color32(255, 255, 255, 255);
            Mission_Button.image.color = new Color32(255, 255, 255, 0);
            MTT_Button.image.color = new Color32(255, 255, 255, 0);
            ShopScreen.SetActive(true);
        }

        void Mission_ButtonOnTap()
        {
            Club_Button.image.color = new Color32(255, 255, 255, 0);
            Shop_Button.image.color = new Color32(255, 255, 255, 0);
            Mission_Button.image.color = new Color32(255, 255, 255, 255);
            MTT_Button.image.color = new Color32(255, 255, 255, 0);
        }

        void MTT_ButtonOnTap()
        {
            Club_Button.image.color = new Color32(255, 255, 255, 0);
            Shop_Button.image.color = new Color32(255, 255, 255, 0);
            Mission_Button.image.color = new Color32(255, 255, 255, 0);
            MTT_Button.image.color = new Color32(255, 255, 255, 255);
        }


       void LobbyPanel_BackButtonOnTap()
        {
            _isPolling = false;
            ClearTables();

            Variant_SelectionPanel.SetActive(true);
            LobbyPanel.SetActive(false);
        }

        private void OnEnable()
        {
            // Show variant selection first; tables are fetched only after a
            // variant is picked in OnVariantSelected.
            _isPolling = false;

            Variant_SelectionPanel.SetActive(true);
            LobbyPanel.SetActive(false);

            RefreshChips().Forget();
        }

        private void OnDisable()
        {
            _isPolling = false;

            if (_preloadHandle.IsValid() && !_isPreloaded)
                Addressables.Release(_preloadHandle);
        }

        // Fetch + display wallet balance on lobby entry.
        public async UniTaskVoid RefreshChips()
        {
            if (chipsText == null) return;

            try
            {
                var data = await AuthManager.Instance.GetChipsAsync();
                DiamondData diamondData = await AuthManager.Instance.GetDiamondsAsync();
                DaimondText.text = FormatChipCount(diamondData.Available);
                chipsText.text = FormatChipCount(data.AvailableChips);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LobbyController] Chips fetch failed: " + e.Message);
            }
        }

        private static string FormatChipCount(long chips)
        {
            if (chips >= 1_000_000) return $"{chips / 1_000_000f:0.#}M";
            if (chips >= 1_000)     return $"{chips / 1_000f:0.#}K";
            return chips.ToString();
        }

        private void LoadVariantJson()
        {
            if (LobbyVariantJson == null)
            {
                Debug.LogError("LobbyVariantJson missing");
                return;
            }

            lobbyVariantResponse =
                JsonConvert.DeserializeObject<LobbyVariantResponse>(
                    LobbyVariantJson.text
                );
        }

        private void GenerateVariantPrefabs()
        {
            ClearVariantPrefabs();
            if (lobbyVariantResponse == null ||
                lobbyVariantResponse.LobbyVariants == null)
                return;

            foreach (LobbyVariantData variant in lobbyVariantResponse.LobbyVariants)
            {
                GameObject obj = Instantiate(variantPrefab, variantContentParent);

                LobbyVariantPrefabScript prefab =
                    obj.GetComponent<LobbyVariantPrefabScript>();

                Sprite sprite = null;

                if (VariantSO != null)
                    sprite = VariantSO.GetVariantSprite(variant.VariantName);

                prefab.Setup(variant, sprite, this);
            }
        }


        private void ClearVariantPrefabs()
        {
            for (int i = variantContentParent.childCount - 1; i >= 0; i--)
            {
                Destroy(variantContentParent.GetChild(i).gameObject);
            }
        }

        public void OnVariantSelected(LobbyVariantData variantData)
        {
            _currentVariant = variantData.VariantKey;
            Variant_SelectionPanel.SetActive(false);
            LobbyPanel.SetActive(true);

            _isPolling = true;
            StartPolling().Forget();
        }

      
       

        private async UniTaskVoid StartPolling()
        {
            // Show the loader only on the first load. Background polls refresh
            // silently — otherwise the loading overlay flashes every 15s.
            bool firstLoad = true;
            while (_isPolling)
            {
                await LoadTables(firstLoad);
                firstLoad = false;
                await UniTask.Delay(15000);
            }
        }

        private void ShowLoading()
        {
            if (loadingIndicator == null) return;

            loadingIndicator.SetActive(true);
            loadingIndicator.transform.DOKill();
            loadingIndicator.transform
                .DORotate(new Vector3(0f, 0f, -360f), 1f, RotateMode.FastBeyond360)
                .SetLoops(-1)
                .SetEase(Ease.Linear);
        }

        private void HideLoading()
        {
            if (loadingIndicator == null) return;

            loadingIndicator.transform.DOKill();
            loadingIndicator.transform.rotation = Quaternion.identity;
            loadingIndicator.SetActive(false);
        }

        private async UniTask LoadTables(bool showLoader = true)
        {
            if (showLoader) ShowLoading();

            try
            {
                var tables = await AuthManager.Instance.GetTablesAsync(
                    _currentVariant
                ).AttachExternalCancellation(destroyCancellationToken);

                await MergeActiveStatus(tables);

                // Leaving the lobby (tapping a table) while these calls are in flight
                // destroys the rows this is about to write into. Unity's destroyed
                // objects still compare non-null as C# references, so the write only
                // fails later, deep inside Setup, as "the object of type 'Button' has
                // been destroyed".
                if (this == null || !isActiveAndEnabled)
                    return;

                UpdateTableList(tables);
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e)
            {
                Debug.LogError("[LobbyController] Table load error: " + e.Message);
            }
            finally
            {
                if (showLoader && this != null) HideLoading();
            }
        }

        // Fetch /active for every table in parallel and merge hand status in.
        private async UniTask MergeActiveStatus(List<TableData> tables)
        {
            if (tables == null || tables.Count == 0) return;

            var tasks = new List<UniTask<TableActiveData>>(tables.Count);
            foreach (var t in tables)
                tasks.Add(AuthManager.Instance.GetTableActiveAsync(t.TableId));

            TableActiveData[] results = await UniTask.WhenAll(tasks);

            for (int i = 0; i < tables.Count; i++)
            {
                var active = results[i];
                if (active == null) continue;

                tables[i].HandInProgress = active.HandInProgress;
                tables[i].GameState = active.GameState;
            }
        }

        private void UpdateTableList(List<TableData> newTables)
        {
            HashSet<string> incomingIds = new();

            foreach (var table in newTables)
            {
                incomingIds.Add(table.TableId);

                // A cached row whose GameObject is gone (scene teardown, or the row was
                // destroyed between refreshes) must be rebuilt, not written into.
                if (_tableMap.TryGetValue(table.TableId, out var existing) &&
                    existing != null)
                {
                    existing.Setup(table, this);
                }
                else
                {
                    GameObject go = Instantiate(tablePrefab, contentParent);
                    LobbyTableItemUI item = go.GetComponent<LobbyTableItemUI>();
                    item.Setup(table, this);

                    // Indexer, not Add: the key can already be here holding a
                    // destroyed row, and Add would throw on the duplicate.
                    _tableMap[table.TableId] = item;
                }
            }

            List<string> keys = new List<string>(_tableMap.Keys);

            foreach (string id in keys)
            {
                if (!incomingIds.Contains(id))
                {
                    Destroy(_tableMap[id].gameObject);
                    _tableMap.Remove(id);
                }
            }

            if (emptyStateLabel != null)
                emptyStateLabel.SetActive(newTables.Count == 0);
        }

        // Opens the shared buy-in popup; onConfirm runs the actual join (seat).
        // Refreshes the player profile first so the popup validates against the
        // live wallet balance (Path A step: GET /api/player/profile).
        public async void ShowBuyIn(string tableId, int min, int max, int smallBlind, int bigBlind, Func<int, UniTask> onConfirm)
        {
            if (buyInView == null)
            {
                Debug.LogError("[LobbyController] buyInView not assigned.");
                return;
            }

            try
            {
                var profile = await AuthManager.Instance.GetPlayerProfileAsync();
                AuthManager.Instance.Session.WalletChips = profile.ChipBalance;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LobbyController] Profile refresh failed: " + e.Message);
            }

            buyInView.Init(tableId, min, max, smallBlind, bigBlind, onConfirm);
        }

        private void ClearTables()
        {
            foreach (var item in _tableMap.Values)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }

            _tableMap.Clear();

            if (emptyStateLabel != null)
                emptyStateLabel.SetActive(false);
        }

        public async UniTask JoinTable()
        {
            if (_isPreloaded)
            {
                await _preloadHandle.Result.ActivateAsync();
            }
            else
            {
                GameSceneManager.Instance.LoadScene("Scene_GameTable");
            }
        }
    }
}