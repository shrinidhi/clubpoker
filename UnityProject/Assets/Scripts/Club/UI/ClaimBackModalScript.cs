using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

public class ClaimBackModalScript : MonoBehaviour
{
    [Header("Header")]
    public Button Close_Button;
    public TextMeshProUGUI MaxClaimBack_Text;

    [Header("Input")]
    public TMP_InputField Amount_InputField;
    public TextMeshProUGUI MemberCount_Text;   // shows "x2"

    [Header("Selected Members")]
    public Transform SelectedMemberList_Content;
    public GameObject SelectedMemberRowPrefab;

    [Header("Footer")]
    public TextMeshProUGUI Total_Text;
    public TextMeshProUGUI Status_Text;
    public Toggle ClaimBackAll_Toggle;
    public Button Confirm_Button;

    [Header("Parent")]
    public TradeViewScript TradeView;

    private List<ClubMember> _selectedMembers = new List<ClubMember>();
    private long _amount = 0;
    private long _maxTotal = 0;

    private void Start()
    {
        Close_Button.onClick.AddListener(OnCloseTap);
        Confirm_Button.onClick.AddListener(OnConfirmTap);
        Amount_InputField.onValueChanged.AddListener(OnAmountChanged);
        ClaimBackAll_Toggle.onValueChanged.AddListener(OnClaimBackAllChanged);
    }

    public void Show(List<ClubMember> members)
    {
        _selectedMembers = members;
        _amount = 0;
        Amount_InputField.text = "";
        Amount_InputField.interactable = true;
        ClaimBackAll_Toggle.isOn = false;
        if (Status_Text != null) Status_Text.gameObject.SetActive(false);

        // max = sum of all selected members' chips
        _maxTotal = 0;
        foreach (var m in members) _maxTotal += m.Chips;

        MemberCount_Text.text = "x" + members.Count;
        MaxClaimBack_Text.text = "Max claim-back: <color=#FFB800>" + _maxTotal.ToString("N0") + "</color>";

        RefreshTotal();
        PopulateMemberList();
        gameObject.SetActive(true);
    }

    private void PopulateMemberList()
    {
        for (int i = SelectedMemberList_Content.childCount - 1; i >= 0; i--)
            Destroy(SelectedMemberList_Content.GetChild(i).gameObject);

        foreach (var member in _selectedMembers)
        {
            var obj = Instantiate(SelectedMemberRowPrefab, SelectedMemberList_Content);
            var row = obj.GetComponent<TradeMemberRowScript>();
            if (row != null) { row.Setup(member, null); row.SetSelected(true); }
        }
    }

    private void OnAmountChanged(string val)
    {
        long.TryParse(val, out _amount);
        if (Status_Text != null) Status_Text.gameObject.SetActive(false);
        RefreshTotal();
    }

    private void OnClaimBackAllChanged(bool isOn)
    {
        Amount_InputField.interactable = !isOn;
        if (isOn) Amount_InputField.text = "";
        if (Status_Text != null) Status_Text.gameObject.SetActive(false);
        RefreshTotal();
    }

    private void RefreshTotal()
    {
        if (ClaimBackAll_Toggle.isOn)
        {
            Total_Text.text = "Total: <color=#FFB800>" + _maxTotal.ToString("N0") + "</color>";
        }
        else
        {
            long total = _amount * _selectedMembers.Count;
            Total_Text.text = "Total: <color=#FFB800>" + total.ToString("N0") + "</color>";
        }
    }

    private void OnConfirmTap()
    {
        bool claimAll = ClaimBackAll_Toggle.isOn;
        if (claimAll && _maxTotal <= 0)
        {
            ShowStatus("Members have no chips to claim back");
            return;
        }
        if (!claimAll)
        {
            if (_amount <= 0)
            {
                ShowStatus("Enter amount or select Claim Back All");
                return;
            }
        }

        Confirm_Button.interactable = false;
        ClaimBack(claimAll).Forget();
    }

    private async UniTaskVoid ClaimBack(bool claimAll)
    {
        try
        {
            var memberIds = new List<string>();
            foreach (var m in _selectedMembers) memberIds.Add(m.Id);

            var res = await ClubChipManager.Instance.ClaimChipsAsync(
                ClubContext.ClubId, memberIds, _amount, claimAll);

            await ClubChipManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);

            if (res?.Results != null)
            {
                var failedList = res.Results.FindAll(r => !r.Success);
                int succeeded  = res.Results.Count - failedList.Count;

                if (succeeded > 0 || failedList.Count == 0)
                {
                    gameObject.SetActive(false);
                    if (TradeView != null)
                    {
                        TradeView.RefreshStatsBar();
                        TradeView.ReloadAfterTrade();
                    }
                }

                if (failedList.Count > 0)
                {
                    string reason = !string.IsNullOrEmpty(failedList[0].Error)
                        ? failedList[0].Error
                        : "Insufficient chips";
                    ShowStatus($"{failedList.Count} member(s) failed: {reason}");
                }
            }
            else
            {
                gameObject.SetActive(false);
                if (TradeView != null)
                {
                    TradeView.RefreshStatsBar();
                    TradeView.ReloadAfterTrade();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ClaimBackModalScript] error: {e.Message}");
        }
        finally
        {
            Confirm_Button.interactable = true;
        }
    }

    private void ShowStatus(string message)
    {
        if (Status_Text == null) return;
        Status_Text.text = message;
        Status_Text.gameObject.SetActive(true);
        FadeOutStatus().Forget();
    }

    private async UniTaskVoid FadeOutStatus()
    {
        await UniTask.Delay(2500);
        if (Status_Text != null) Status_Text.gameObject.SetActive(false);
    }

    private void OnCloseTap()
    {
        gameObject.SetActive(false);
    }
}
