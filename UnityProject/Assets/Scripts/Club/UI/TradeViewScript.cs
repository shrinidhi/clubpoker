using System.Collections.Generic;
using System.Linq;
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
    public TMP_Dropdown SortBy_Dropdown;  // 0=Chips, 1=Time Joined

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

    // /api/clubs/{id}/members ignores the search param today: the request went out
    // on every keystroke and came back as the full list, so typing did nothing.
    // Until it lands, search filters the fetched page locally.
    //
    // Flip this to true the day the backend honours it — nothing else changes.
    // Worth doing, because GetMembersAsync fetches limit=100 with no paging, so a
    // local search can't see member 101 no matter what is typed.
    // static readonly, not const: a const folds at compile time and every branch
    // behind it turns into an unreachable-code warning.
    private static readonly bool ServerSideSearch = false;

    // The last page fetched from the server, unfiltered. Only used as the source
    // for the local filter; with ServerSideSearch on, the server already filtered.
    private List<ClubMember> _allMembers = new List<ClubMember>();

    private void Start()
    {
        Search_InputField.onValueChanged.AddListener(OnSearchChanged);
        GroupByRole_Toggle.onValueChanged.AddListener(_ => RenderMembers(Search_InputField.text));
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
        // Through ReloadMembers so the fetch honours whatever the sort dropdown
        // still shows from the last visit.
        ReloadMembers();
    }

    private async UniTaskVoid LoadMembers(string sortBy)
    {
        ClearList();

        try
        {
            string search = ServerSideSearch ? Search_InputField.text : null;

            // Grouping is done locally in RenderMembers, so groupByRole isn't sent.
            var res = await ClubManager.Instance.GetMembersAsync(
                ClubContext.ClubId, search, sortBy: sortBy);

            if (res?.Members == null) return;

            _allMembers = res.Members;

            var placeholder = Search_InputField.placeholder.GetComponent<TextMeshProUGUI>();
            if (placeholder != null) placeholder.text = $"Search member({res.Total})";

            // Server-filtered results are rendered whole — filtering them again
            // locally would drop rows the server matched on a field we don't check.
            RenderMembers(ServerSideSearch ? null : Search_InputField.text);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TradeViewScript] LoadMembers error: {e.Message}");
        }
    }

    // Rebuilds the rows from the cached page, keeping only members matching the
    // search box. Server order is preserved so the sort dropdown still rules —
    // with Group by role on, it still rules inside each role block, since
    // OrderBy is a stable sort.
    private void RenderMembers(string search)
    {
        ClearRows();

        string term = search?.Trim();

        IEnumerable<ClubMember> members = _allMembers;
        if (GroupByRole_Toggle.isOn)
            members = members.OrderBy(m => GetRoleOrder(m.Role));

        foreach (var member in members)
        {
            if (!MatchesSearch(member, term)) continue;

            var obj = Instantiate(MemberRowPrefab, MemberList_Content);
            var row = obj.GetComponent<TradeMemberRowScript>();
            row.Setup(member, OnMemberSelectionChanged);

            // Setup clears the toggle, so a member picked before the search was
            // typed came back unchecked while still counting toward the batch.
            row.SetSelected(_selectedMembers.Exists(m => m.Id == member.Id));

            _rows.Add(row);
        }
    }

    // Username, nickname or the short ID shown on the row — the three things
    // visible to someone looking at the list.
    private static bool MatchesSearch(ClubMember member, string term)
    {
        if (string.IsNullOrEmpty(term)) return true;

        return Contains(member.Username, term)
            || Contains(member.Nickname, term)
            || Contains(member.Id, term);
    }

    private static bool Contains(string value, string term) =>
        !string.IsNullOrEmpty(value) &&
        value.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0;

    // Club hierarchy top-down; same order as the Members panel.
    private static int GetRoleOrder(string role)
    {
        string normalized = string.IsNullOrEmpty(role)
            ? "MEMBER"
            : role.Trim().Replace(" ", "_").Replace("-", "_").ToUpperInvariant();

        switch (normalized)
        {
            case "CREATOR":       return 0;
            case "MANAGER":       return 1;
            case "TABLE_MANAGER": return 2;
            case "SUPER_AGENT":   return 3;
            case "AGENT":         return 4;
            case "MEMBER":        return 5;
            default:              return 6;
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
        // Local filter over the cached page — no request, so no debounce.
        if (!ServerSideSearch)
        {
            RenderMembers(search);
            return;
        }

        // Server-side: debounce, or every keystroke is its own request.
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

    // Sort is the server's call, so it refetches. Indexes match SortBy_Dropdown.
    private static readonly string[] SortKeys = { "chips", "joinedAt" };

    private void ReloadMembers()
    {
        int i = Mathf.Clamp(SortBy_Dropdown.value, 0, SortKeys.Length - 1);
        LoadMembers(SortKeys[i]).Forget();
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

    // Fresh data from the server: rows and selection both go.
    private void ClearList()
    {
        ClearRows();
        _selectedMembers.Clear();
        SetBottomButtonsInteractable(false);
    }

    // Re-render of the same data (search, grouping): keep the selection so
    // RenderMembers can tick the rows back on.
    private void ClearRows()
    {
        _rows.Clear();
        for (int i = MemberList_Content.childCount - 1; i >= 0; i--)
            Destroy(MemberList_Content.GetChild(i).gameObject);
    }
}
