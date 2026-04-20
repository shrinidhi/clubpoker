using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

namespace ClubPoker.UI
{
    public class LobbyView : MonoBehaviour
    {
        [Header("UI")]
        public Transform contentParent;
        public GameObject tablePrefab;

        [Header("Filter")]
        public TMP_Dropdown Variant_DropDown;
        public TMP_InputField Small_Blind_InputField;
        public TMP_InputField Big_Blind_InputField;
        public Button Ok_Button;

        [Header("Other")]
        public Button Back_Button;
        public BuyInView BuyInView;

        private Dictionary<string, LobbyTableItemUI> tableMap = new();
        private bool isPolling;

        private string currentVariant = "all";
        private int currentMinBlind = 0;
        private int currentMaxBlind = 0;

        private void Start()
        {
            Back_Button.onClick.AddListener(() => gameObject.SetActive(false));
            Ok_Button.onClick.AddListener(OnFilterApply);

            SetupDropdown();
        }

        private void OnEnable()
        {
            isPolling = true;
            StartPolling().Forget();
            Variant_DropDown.value = 0;   
            Variant_DropDown.RefreshShownValue();

            currentVariant = "all";    

            Small_Blind_InputField.text = "";
            Big_Blind_InputField.text = "";

        }

        private void OnDisable()
        {
            isPolling = false;
        }

        void SetupDropdown()
        {
            Variant_DropDown.ClearOptions();
            Variant_DropDown.AddOptions(new List<string>
            {
                "All",
                "Texas Hold'em",
                "Omaha",
                "Omaha 6"
            });
        }

        void OnFilterApply()
        {
            currentVariant = GetVariant();
            currentMinBlind = GetSmallBlind();
            currentMaxBlind = GetBigBlind();

            Debug.Log($"Filter Applied: {currentVariant}, {currentMinBlind}-{currentMaxBlind}");

            LoadTables().Forget();
        }

        string GetVariant()
        {
            switch (Variant_DropDown.value)
            {
                case 1: return "texas_holdem";
                case 2: return "omaha";
                case 3: return "omaha_six";
                default: return "all";
            }
        }

        int GetSmallBlind()
        {
            int.TryParse(Small_Blind_InputField.text, out int val);
            return val;
        }

        int GetBigBlind()
        {
            int.TryParse(Big_Blind_InputField.text, out int val);
            return val;
        }

        async UniTaskVoid StartPolling()
        {
            while (isPolling)
            {
                await LoadTables();
                await UniTask.Delay(15000);
            }
        }

        async UniTask LoadTables()
        {
            try
            {
                var tables = await AuthManager.Instance.GetTablesAsync(
                    currentVariant,
                    currentMinBlind,
                    currentMaxBlind
                );

                UpdateTableList(tables);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Table Load Error: " + e.Message);
            }
        }

        void UpdateTableList(List<TableData> newTables)
        {
            HashSet<string> incomingIds = new();

            foreach (var table in newTables)
            {
                incomingIds.Add(table.TableId);

                if (tableMap.TryGetValue(table.TableId, out var ui))
                {
                  
                    ui.Setup(table, BuyInView);
                }
                else
                {
                    
                    var go = Instantiate(tablePrefab, contentParent);
                    var item = go.GetComponent<LobbyTableItemUI>();

                    item.Setup(table, BuyInView);
                    tableMap.Add(table.TableId, item);
                }
            }

          
            var keys = new List<string>(tableMap.Keys);

            foreach (var id in keys)
            {
                if (!incomingIds.Contains(id))
                {
                    Destroy(tableMap[id].gameObject);
                    tableMap.Remove(id);
                }
            }
        }
    }
}