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

namespace ClubPoker.Lobby
{
    public class LobbyController : MonoBehaviour
    {
        [Header("Table List")]
        [SerializeField] private Transform contentParent;
        [SerializeField] private GameObject tablePrefab;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private GameObject emptyStateLabel;

        [Header("Filters")]
        [SerializeField] private TMP_Dropdown variantDropdown;
        [SerializeField] private TMP_InputField smallBlindInput;
        [SerializeField] private TMP_InputField bigBlindInput;
        [SerializeField] private Button applyFilterBtn;

        [Header("Navigation")]
        [SerializeField] private Button backButton;

        private readonly Dictionary<string, LobbyTableItemUI> _tableMap = new();
        private AsyncOperationHandle<SceneInstance> _preloadHandle;
        private bool _isPreloaded;
        private bool _isPolling;

        private string _currentVariant = "all";
        private int _currentMinBlind;
        private int _currentMaxBlind;

        private void Start()
        {
            backButton.onClick.AddListener(() => GameSceneManager.Instance.LoadScene("Scene_MainMenu"));
            applyFilterBtn.onClick.AddListener(OnFilterApply);
            SetupDropdown();
        }

        private void OnEnable()
        {
            _isPolling = true;

            variantDropdown.value = 0;
            variantDropdown.RefreshShownValue();
            _currentVariant = "all";
            _currentMinBlind = 0;
            _currentMaxBlind = 0;
            smallBlindInput.text = "";
            bigBlindInput.text = "";

            StartPolling().Forget();
        }

        private void OnDisable()
        {
            _isPolling = false;

            if (_preloadHandle.IsValid() && !_isPreloaded)
                Addressables.Release(_preloadHandle);
        }

        private void SetupDropdown()
        {
            variantDropdown.ClearOptions();
            variantDropdown.AddOptions(new List<string>
            {
                "All",
                "Texas Hold'em",
                "Omaha",
                "Omaha 6"
            });
        }

        private void OnFilterApply()
        {
            string variant = GetVariant();
            int minBlind = GetSmallBlind();
            int maxBlind = GetBigBlind();

            bool variantChanged = variant != "all";
            bool minFilled = !string.IsNullOrWhiteSpace(smallBlindInput.text);
            bool maxFilled = !string.IsNullOrWhiteSpace(bigBlindInput.text);

            if (!variantChanged && !minFilled && !maxFilled) return;

            if (minFilled != maxFilled) return;

            if (minFilled && maxFilled && minBlind > maxBlind) return;

            _currentVariant = variant;
            _currentMinBlind = minBlind;
            _currentMaxBlind = maxBlind;
            LoadTables().Forget();
        }

        private string GetVariant()
        {
            return variantDropdown.value switch
            {
                1 => "texas_holdem",
                2 => "omaha",
                3 => "omaha_six",
                _ => "all"
            };
        }

        private int GetSmallBlind()
        {
            int.TryParse(smallBlindInput.text, out int val);
            return val;
        }

        private int GetBigBlind()
        {
            int.TryParse(bigBlindInput.text, out int val);
            return val;
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
            loadingIndicator.transform.DORotate(new Vector3(0f, 0f, -360f), 1f, RotateMode.FastBeyond360)
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
                    var go = Instantiate(tablePrefab, contentParent);
                    var item = go.GetComponent<LobbyTableItemUI>();
                    item.Setup(table);
                    _tableMap.Add(table.TableId, item);
                }
            }

            var keys = new List<string>(_tableMap.Keys);
            foreach (var id in keys)
            {
                if (!incomingIds.Contains(id))
                {
                    Destroy(_tableMap[id].gameObject);
                    _tableMap.Remove(id);
                }
            }

            if (emptyStateLabel != null) emptyStateLabel.SetActive(newTables.Count == 0);
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
