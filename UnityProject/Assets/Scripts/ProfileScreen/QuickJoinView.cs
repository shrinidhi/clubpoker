using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking;
using ClubPoker.Game;
using TMPro;
using System.Collections.Generic;
using System;
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
            quickJoinButton.onClick.AddListener(() => OnQuickJoinClicked().Forget());
            Close_Button.onClick.AddListener(Close_ButtonOnTap);

            LoadVariants();
        }

        private void OnEnable()
        {
            TableJoinHandler.OnJoinFailed += OnJoinFailed;
        }

        private void OnDisable()
        {
            TableJoinHandler.OnJoinFailed -= OnJoinFailed;
        }

        private void OnJoinFailed(string message)
        {
            ClubPoker.Core.ToastEvents.Show("Could not connect to table. Please try again.");
            loadingPanel.SetActive(false);
            quickJoinButton.interactable = true;
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
                    defaultIndex = i;
            }

            variantDropdown.AddOptions(options);
            variantDropdown.value = defaultIndex;
            variantDropdown.RefreshShownValue();
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

        private async UniTaskVoid OnQuickJoinClicked()
        {
            loadingPanel.SetActive(true);
            quickJoinButton.interactable = false;

            try
            {
                string variant = GetSelectedVariant();

                var table = await AuthManager.Instance.QuickJoinAsync(variant);

                Debug.Log("✅ Table Found: " + table.TableId);

                await AuthManager.Instance.JoinTableAsync(table.TableId, 1000);

                TableJoinHandler.Instance.JoinTable(table.TableId);

                await UniTask.Delay(1500);

                if (UnityBotRunner.Instance != null)
                    await UnityBotRunner.Instance.StartBots(table.TableId, table.MaxPlayers);

                await UniTask.Delay(1500);

                await AuthManager.Instance.StartTableAsync(table.TableId, 3);
            }
            catch (LobbyException e)
            {
                if (e.Code == "L001")
                    Debug.Log("❌ No Tables Available");
                else
                    Debug.LogError("Lobby Error: " + e.Message);
            }
            catch (Exception e)
            {
                Debug.LogError("QuickJoin Error: " + e.Message);
            }
            finally
            {
                loadingPanel.SetActive(false);
                quickJoinButton.interactable = true;
            }
        }

        string GetSelectedVariant()
        {
            int index = variantDropdown.value;
            return variantList[index].id;
        }
    }
}
