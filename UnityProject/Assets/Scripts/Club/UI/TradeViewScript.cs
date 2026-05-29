using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

public class TradeViewScript : MonoBehaviour
{
    [Header("Stats Bar")]
    public TextMeshProUGUI AvailableChips_Text;
    public TextMeshProUGUI AgentsCredit_Text;
    public TextMeshProUGUI MembersChips_Text;

    [Header("Search & Filters")]
    public TMP_InputField Search_InputField;
    public Toggle GroupByRole_Toggle;
    public TMP_Dropdown SortBy_Dropdown;  // 0=Chips, 1=Winnings

    [Header("Member List")]
    public Transform MemberList_Content;
    public GameObject MemberRowPrefab;

    [Header("Bottom Buttons")]
    public Button SendOut_Button;
    public Button ClaimBack_Button;
    public Button SendTicket_Button;

    [Header("Modals")]
    public SendOutModalScript SendOutModal;
    public ClaimBackModalScript ClaimBackModal;
    public AddChipsModalScript AddChipsModal;

    [Header("Stats Bar Buttons")]
    public Button AvailableChips_Button;


    private List<TradeMemberRowScript> _rows = new List<TradeMemberRowScript>();
    private List<ClubMember> _selectedMembers = new List<ClubMember>();

    private void Start()
    {
        Search_InputField.onValueChanged.AddListener(OnSearchChanged);
        GroupByRole_Toggle.onValueChanged.AddListener(_ => ReloadMembers());
        SortBy_Dropdown.onValueChanged.AddListener(_ => ReloadMembers());
        if (AvailableChips_Button != null)
            AvailableChips_Button.onClick.AddListener(() => AddChipsModal.Show());
        SendOut_Button.onClick.AddListener(OnSendOutTap);
        ClaimBack_Button.onClick.AddListener(OnClaimBackTap);
        SendTicket_Button.onClick.AddListener(OnSendTicketTap);

        SetBottomButtonsInteractable(false);
    }

    public void RefreshStatsBar()
    {
        AvailableChips_Text.text = ClubContext.PoolChips.ToString("N0");
        AgentsCredit_Text.text   = ClubContext.AgentsCredit.ToString("N0");
        MembersChips_Text.text   = ClubContext.MembersChips.ToString("N0");
    }

    public void Init()
    {
        RefreshStatsBar();
        _selectedMembers.Clear();
        Search_InputField.text = "";
        SetBottomButtonsInteractable(false);
        LoadMembers().Forget();
    }

    private async UniTaskVoid LoadMembers(string search = null, bool groupByRole = false, string sortBy = null)
    {
        ClearList();

        try
        {
            var res = await ClubChipManager.Instance.GetMembersAsync(
                ClubContext.ClubId, search, groupByRole, sortBy);

            if (res?.Members == null) return;

            var placeholder = Search_InputField.placeholder.GetComponent<TextMeshProUGUI>();
            if (placeholder != null) placeholder.text = $"Search member({res.Total})";

            foreach (var member in res.Members)
            {
                var obj = Instantiate(MemberRowPrefab, MemberList_Content);
                var row = obj.GetComponent<TradeMemberRowScript>();
                row.Setup(member, OnMemberSelectionChanged);
                _rows.Add(row);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TradeViewScript] LoadMembers error: {e.Message}");
        }
    }

    private void OnMemberSelectionChanged(string memberId, bool isSelected)
    {
        if (isSelected)
        {
            var row = _rows.Find(r => r.MemberId == memberId);
            if (row != null && !_selectedMembers.Exists(m => m.Id == memberId))
                _selectedMembers.Add(row.Member);
        }
        else
        {
            _selectedMembers.RemoveAll(m => m.Id == memberId);
        }

        SetBottomButtonsInteractable(_selectedMembers.Count > 0);
    }

    private void OnSendOutTap()
    {
        if (_selectedMembers.Count == 0) return;
        SendOutModal.Show(_selectedMembers);
    }

    private void OnClaimBackTap()
    {
        if (_selectedMembers.Count == 0) return;
        ClaimBackModal.Show(_selectedMembers);
    }

    private void OnSendTicketTap()
    {
        // switch CashierPanel to Chips Request tab
        var cashier = GetComponentInParent<CashierPanelScript>(true);
        if (cashier != null) cashier.ShowChipsRequestTab();
    }

    private System.Threading.CancellationTokenSource _searchCts;

    private void OnSearchChanged(string search)
    {
        _searchCts?.Cancel();
        _searchCts = new System.Threading.CancellationTokenSource();
        DebounceSearch(_searchCts.Token).Forget();
    }

    private async UniTaskVoid DebounceSearch(System.Threading.CancellationToken token)
    {
        try
        {
            await UniTask.Delay(400, cancellationToken: token);
            ReloadMembers();
        }
        catch (System.OperationCanceledException) { }
    }

    private void ReloadMembers()
    {
        string[] sortKeys = { "chips", "winnings" };
        string sortBy = sortKeys[SortBy_Dropdown.value];
        LoadMembers(Search_InputField.text, GroupByRole_Toggle.isOn, sortBy).Forget();
    }

    private void SetBottomButtonsInteractable(bool state)
    {
        SendOut_Button.interactable    = state && ClubContext.IsAdmin;
        ClaimBack_Button.interactable  = state && ClubContext.IsAdmin;
        SendTicket_Button.interactable = true;
    }

    public void ShowAddChipsSuccess(long amount)
    {
        var cashier = GetComponentInParent<CashierPanelScript>(true);
        if (cashier != null) cashier.ShowToast($"Added {amount:N0} chips to club pool").Forget();
    }

    public void ReloadAfterTrade()
    {
        ReloadMembers();
    }

    private void ClearList()
    {
        _rows.Clear();
        _selectedMembers.Clear();
        for (int i = MemberList_Content.childCount - 1; i >= 0; i--)
            Destroy(MemberList_Content.GetChild(i).gameObject);
    }
}
