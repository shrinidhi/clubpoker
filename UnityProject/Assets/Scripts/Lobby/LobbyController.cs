using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ClubPoker.Core;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
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

       

        private readonly Dictionary<string, LobbyTableItemUI> _tableMap = new();
        private AsyncOperationHandle<SceneInstance> _preloadHandle;
        private bool _isPreloaded;
        private bool _isPolling;

        private string _currentVariant = "all";
        private int _currentMinBlind;
        private int _currentMaxBlind;

        private LobbyVariantResponse lobbyVariantResponse;

        [SerializeField] private VariantSO VariantSO;

        [SerializeField] private GameObject Variant_SelectionPanel;
        [SerializeField] private GameObject  LobbyPanel;
        [SerializeField] private Button   LobbyPanel_BackButton;

        [Header("Botton Button")]
        [SerializeField] private Button Club_Button;
        [SerializeField] private Button Shop_Button;
        [SerializeField] private Button Mission_Button;
        [SerializeField] private Button MTT_Button;

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
            Shop_Button.image.color = new Color32(255, 255, 255, 255);
            Mission_Button.image.color = new Color32(255, 255, 255, 0);
            MTT_Button.image.color = new Color32(255, 255, 255, 0);
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
            Variant_SelectionPanel.SetActive(true);
            LobbyPanel.SetActive(false);
        }

        private void OnEnable()
        {
            _isPolling = true;

            _currentVariant = "all";
            _currentMinBlind = 5;
            _currentMaxBlind = 10;

            StartPolling().Forget();
        }

        private void OnDisable()
        {
            _isPolling = false;

            if (_preloadHandle.IsValid() && !_isPreloaded)
                Addressables.Release(_preloadHandle);
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
            _currentMinBlind = 5;
            _currentMaxBlind = 10;
            Variant_SelectionPanel.SetActive(false);
            LobbyPanel.SetActive(true);
            LoadTables().Forget();
        }

      
       

        private async UniTaskVoid StartPolling()
        {
            while (_isPolling)
            {
                await LoadTables();
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

        private async UniTask LoadTables()
        {
            ShowLoading();

            try
            {
                var tables = await AuthManager.Instance.GetTablesAsync(
                    _currentVariant,
                    _currentMinBlind,
                    _currentMaxBlind
                );

                UpdateTableList(tables);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[LobbyController] Table load error: " + e.Message);
            }
            finally
            {
                HideLoading();
            }
        }

        private void UpdateTableList(List<TableData> newTables)
        {
            HashSet<string> incomingIds = new();

            foreach (var table in newTables)
            {
                incomingIds.Add(table.TableId);

                if (_tableMap.TryGetValue(table.TableId, out var existing))
                {
                    existing.Setup(table);
                }
                else
                {
                    GameObject go = Instantiate(tablePrefab, contentParent);
                    LobbyTableItemUI item = go.GetComponent<LobbyTableItemUI>();
                    item.Setup(table);
                    _tableMap.Add(table.TableId, item);
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