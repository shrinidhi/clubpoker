using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking;
using TMPro;
using System.Collections.Generic;
using ClubPoker.Auth;

namespace ClubPoker.UI
{
    [System.Serializable]
    public class VariantData
    {
        public string id;
        public string name;

        public VariantData(string id, string name)
        {
            this.id = id;
            this.name = name;
        }
    }

    public class QuickJoinView : MonoBehaviour
    {
        [Header("UI")]
        public Button quickJoinButton;
        public GameObject loadingPanel;
        public TMP_Dropdown variantDropdown;
        public Button Close_Button;

        private List<VariantData> variantList = new List<VariantData>();

        private void Start()
        {
            quickJoinButton.onClick.AddListener(OnQuickJoinClicked);
            Close_Button.onClick.AddListener(Close_ButtonOnTap);

            LoadVariants();
        }

        void Close_ButtonOnTap()
        {
            gameObject.SetActive(false);
        }

        void LoadVariants()
        {
            variantList = GetManualVariants();

            variantDropdown.ClearOptions();

            List<string> options = new List<string>();

            int defaultIndex = 0;

            for (int i = 0; i < variantList.Count; i++)
            {
                options.Add(variantList[i].name);

                if (variantList[i].id == "texas_holdem")
                {
                    defaultIndex = i;
                }
            }

            variantDropdown.AddOptions(options);

            variantDropdown.value = defaultIndex;
            variantDropdown.RefreshShownValue();

            Debug.Log("✅ Variants Loaded (Manual)");
        }

        List<VariantData> GetManualVariants()
        {
            return new List<VariantData>()
            {
                new VariantData("texas_holdem", "Texas Hold'em"),
                new VariantData("omaha", "Omaha"),
                new VariantData("pineapple", "Pineapple"),
                new VariantData("short_deck", "Short Deck")
            };
        }
        async void OnQuickJoinClicked()
        {
            loadingPanel.SetActive(true);

            try
            {
                string variant = GetSelectedVariant();

                var response = await AuthManager.Instance.QuickJoinAsync(variant);

                Debug.Log("✅ Table Found: " + response.TableId);

                // 👉 Scene Load
                // GameSceneManager.Instance.LoadScene("TableScene");
            }
            catch (LobbyException e)
            {
                if (e.Code == "L001")
                {
                    ShowNoTable();
                }
                else
                {
                    Debug.LogError("Lobby Error: " + e.Message);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error: " + e.Message);
            }
            finally
            {
                loadingPanel.SetActive(false);
            }
        }

      
        string GetSelectedVariant()
        {
            int index = variantDropdown.value;

            if (index == 0)
                return null; 

            return variantList[index - 1].id;
        }
        void ShowNoTable()
        {
            Debug.Log("❌ No Tables Available");

           
            // "No tables found"
            // Button: Create Table
        }
    }
}