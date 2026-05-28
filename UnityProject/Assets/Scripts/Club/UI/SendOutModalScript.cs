using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

public class SendOutModalScript : MonoBehaviour
{
    [Header("Header")]
    public Button Close_Button;
    public TextMeshProUGUI PoolChips_Text;

    [Header("Input")]
    public TMP_InputField Amount_InputField;
    public TextMeshProUGUI MemberCount_Text;   // shows "x2" next to input

    [Header("Selected Members")]
    public Transform SelectedMemberList_Content;
    public GameObject SelectedMemberRowPrefab;

    [Header("Footer")]
    public TextMeshProUGUI Total_Text;
    public TextMeshProUGUI Status_Text;
    public Button Confirm_Button;

    [Header("Parent")]
    public TradeViewScript TradeView;

    private List<ClubMember> _selectedMembers = new List<ClubMember>();
    private long _amount = 0;

    private void Start()
    {
        Close_Button.onClick.AddListener(OnCloseTap);
        Confirm_Button.onClick.AddListener(OnConfirmTap);
        Amount_InputField.onValueChanged.AddListener(OnAmountChanged);
    }

    public void Show(List<ClubMember> members)
    {
        _selectedMembers = members;
        _amount = 0;
        Amount_InputField.text = "";
        PoolChips_Text.text = ClubContext.PoolChips.ToString("N0");
        MemberCount_Text.text = "x" + members.Count;
        if (Status_Text != null) Status_Text.gameObject.SetActive(false);
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

    private void RefreshTotal()
    {
        long total = _amount * _selectedMembers.Count;
        Total_Text.text = "Total: <color=#FFB800>" + total.ToString("N0") + "</color>";
    }

    private void OnConfirmTap()
    {
        if (_amount <= 0) return;

        long total = _amount * _selectedMembers.Count;
        if (total > ClubContext.PoolChips)
        {
            if (Status_Text != null)
            {
                ShowStatus($"Insufficient pool chips. Available: {ClubContext.PoolChips:N0}");
            }
            return;
        }

        if (Status_Text != null) Status_Text.gameObject.SetActive(false);
        Confirm_Button.interactable = false;
        SendOut().Forget();
    }

    private async UniTaskVoid SendOut()
    {
        try
        {
            var memberIds = new List<string>();
            foreach (var m in _selectedMembers) memberIds.Add(m.Id);

            var res = await ClubChipManager.Instance.SendChipsAsync(
                ClubContext.ClubId, memberIds, _amount);

            await ClubChipManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);

            if (res?.Results != null)
            {
                int failed = res.Results.FindAll(r => !r.Success).Count;
                if (failed == 0)
                {
                    gameObject.SetActive(false);
                    if (TradeView != null)
                    {
                        TradeView.RefreshStatsBar();
                        TradeView.ReloadAfterTrade();
                    }
                }
                else
                {
                    // partial failure — show which failed
                    if (Status_Text != null)
                    {
                        ShowStatus($"{failed} member(s) failed: Insufficient pool chips");
                    }
                }
            }
            else
            {
                gameObject.SetActive(false);
                if (TradeView != null) TradeView.ReloadAfterTrade();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SendOutModalScript] error: {e.Message}");
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
