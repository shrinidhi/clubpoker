using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Networking.Models;
using ClubPoker.Auth;
using System;

public class ClubTablePrefabScript : MonoBehaviour
{
    public Text VariantName;
    public Text SB_BB_Text;
    public Text PlayerSeat_Text;

    private ClubTableData tableData;

    public Button DeleteButton;

    private Action<ClubTableData> onDeleteClick;

    public void Setup(
        ClubTableData data,
        Action<ClubTableData> deleteCallback = null)
    {
        tableData = data;
        onDeleteClick = deleteCallback;

        VariantName.text = data.Name;
        SB_BB_Text.text = data.SmallBlind + "/" + data.BigBlind;
        PlayerSeat_Text.text = data.PlayerCount + "/" + data.MaxSeats;

        bool isMyCreatedTable =
            AuthManager.Instance != null &&
            AuthManager.Instance.Session != null &&
            data.CreatedById == AuthManager.Instance.Session.Id;

        if (DeleteButton != null)
        {
            DeleteButton.gameObject.SetActive(isMyCreatedTable);

            DeleteButton.onClick.RemoveAllListeners();

            if (isMyCreatedTable)
                DeleteButton.onClick.AddListener(OnDeleteButtonClick);
        }
    }

    private void OnDeleteButtonClick()
    {
        if (tableData == null)
            return;

        onDeleteClick?.Invoke(tableData);
    }
}