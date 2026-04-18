using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using UnityEngine.UI;
namespace ClubPoker.UI
{
    public class LobbyView : MonoBehaviour
    {
        public Transform contentParent;
        public GameObject tablePrefab;

        private Dictionary<string, LobbyTableItemUI> tableMap = new();

        private bool isPolling;

        public Button Back_Button;
        public BuyInView BuyInView;

        private void Start()
        {
            Back_Button.onClick.AddListener(Back_ButtonOnTap);
        }

        private void OnEnable()
        {
            isPolling = true;
            StartPolling().Forget();
        }

        private void OnDisable()
        {
            isPolling = false;
        }
        void Back_ButtonOnTap()
        {
            gameObject.SetActive(false);
        }
        private async UniTaskVoid StartPolling()
        {
            while (isPolling)
            {
                await LoadTables();
                await UniTask.Delay(15000);
            }
        }

        private async UniTask LoadTables()
        {
            var tables = await AuthManager.Instance.GetTablesAsync("all", 0, 0);
            UpdateTableList(tables);
        }

        private void UpdateTableList(List<TableData> newTables)
        {
            HashSet<string> incomingIds = new();

            foreach (var table in newTables)
            {
                incomingIds.Add(table.TableId);

                if (tableMap.TryGetValue(table.TableId, out var ui))
                {
                    ui.Setup(table,BuyInView);
                }
                else
                {
                    var go = Instantiate(tablePrefab, contentParent);

                    if (go == null)
                    {
                        continue;
                    }

                    var item = go.GetComponent<LobbyTableItemUI>();

                    if (item == null)
                    {
                        continue;
                    }

                    item.Setup(table,BuyInView);
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