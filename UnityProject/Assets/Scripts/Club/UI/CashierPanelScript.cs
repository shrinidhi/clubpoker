using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using ClubPoker.Core;

public class CashierPanelScript : MonoBehaviour
{
    [Header("Header")]
    public Button Back_Button;

    [Header("Tab Buttons")]
    public Button TradeTab_Button;
    public Button TradeRecordTab_Button;
    public Button ChipsRequestTab_Button;

    [Header("Tab Texts")]
    public TextMeshProUGUI TradeTab_Text;
    public TextMeshProUGUI TradeRecordTab_Text;
    public TextMeshProUGUI ChipsRequestTab_Text;

    [Header("Badges")]
    public GameObject ChipsRequestBadge;          // red dot on Chips Request tab
    public TextMeshProUGUI ChipsRequestBadge_Text; // optional count label inside badge

    [Header("Tab Sprites")]
    public Sprite TabActive_Sprite;
    public Sprite TabInactive_Sprite;

    [Header("Views")]
    public GameObject TradeView;
    public GameObject TradeRecordView;
    public GameObject ChipsRequestView;

    [Header("Scripts")]
    public TradeViewScript TradeViewScript;
    public TradeRecordViewScript TradeRecordViewScript;
    public ChipsRequestViewScript ChipsRequestViewScript;

    [Header("Toast")]
    public TextMeshProUGUI Toast_Text;

    private int _activeTab = 0;
    private System.Threading.CancellationTokenSource _pollCts;

    private void Start()
    {
        Back_Button.onClick.AddListener(OnBackTap);
        TradeTab_Button.onClick.AddListener(() => ShowTab(0));
        TradeRecordTab_Button.onClick.AddListener(() => ShowTab(1));
        ChipsRequestTab_Button.onClick.AddListener(() => ShowTab(2));
    }

    /// <summary>Opens on <paramref name="tab"/> when given (0 trade, 1 records,
    /// 2 chip requests), else the role's default.</summary>
    public void Init(int tab = -1)
    {
        InitAsync(tab).Forget();
    }

    private async UniTaskVoid InitAsync(int tab)
    {
        try
        {
            await ClubManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CashierPanelScript] summary fetch error: {e.Message}");
        }

        RefreshBadge();
        int defaultTab = ClubContext.CanManageChips ? 0 : 2;
        ShowTab(tab >= 0 ? tab : defaultTab);

        _pollCts = new System.Threading.CancellationTokenSource();
        PollRequests(_pollCts.Token).Forget();
    }

    /// <summary>Raised when the Cashier closes — requests may have been handled
    /// inside, so the club screen re-checks its Cashier red dot.</summary>
    public static event System.Action OnClosed;

    private void OnEnable()
    {
        ClubSocketHandler.OnChipRequestReceived += HandleChipRequestReceived;
    }

    private void OnDisable()
    {
        ClubSocketHandler.OnChipRequestReceived -= HandleChipRequestReceived;

        _pollCts?.Cancel();
        _pollCts = null;

        OnClosed?.Invoke();
    }

    // CP-284 — a member asked for chips while the Cashier is open. PendingCount
    // was already bumped by the socket handler; redraw the badge and, if the
    // requests tab is showing, reload its list.
    private void HandleChipRequestReceived(ChipRequestReceivedEvent payload)
    {
        RefreshBadge();

        if (_activeTab == 2 && ChipsRequestViewScript != null)
            ChipsRequestViewScript.Init();
    }

    private async UniTaskVoid PollRequests(System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await UniTask.Delay(30000, cancellationToken: token);

                int prevCount = ClubContext.PendingCount;
                await ClubManager.Instance.GetChipsSummaryAsync(ClubContext.ClubId);
                RefreshBadge();

                if (ClubContext.PendingCount > prevCount)
                {
                    int newCount = ClubContext.PendingCount - prevCount;
                    if (_activeTab == 2 && ChipsRequestViewScript != null)
                        ChipsRequestViewScript.Init();
                    else
                        ShowRequestToast(newCount);
                }
            }
            catch (System.OperationCanceledException) { break; }
            catch (System.Exception e)
            {
                Debug.LogError($"[CashierPanelScript] poll error: {e.Message}");
            }
        }
    }

    private void ShowRequestToast(int count)
    {
        string msg = count == 1 ? Loc.Get("chipreq.new_one") : Loc.Get("chipreq.new_many", ("count", count));
        ShowToast(msg).Forget();
    }

    public async UniTaskVoid ShowToast(string message)
    {
        if (Toast_Text == null) return;
        Toast_Text.text = message;
        Toast_Text.gameObject.SetActive(true);
        await UniTask.Delay(3000);
        if (Toast_Text != null) Toast_Text.gameObject.SetActive(false);
    }

    private void ShowTab(int index)
    {
        _activeTab = index;
        TradeView.SetActive(index == 0);
        TradeRecordView.SetActive(index == 1);
        ChipsRequestView.SetActive(index == 2);

        SetTabVisuals(index);

        if (index == 0 && TradeViewScript != null)
            TradeViewScript.Init();
        else if (index == 1 && TradeRecordViewScript != null)
            TradeRecordViewScript.Init();
        else if (index == 2 && ChipsRequestViewScript != null)
            ChipsRequestViewScript.Init();
    }

    private void SetTabVisuals(int activeIndex)
    {
        SetTab(TradeTab_Button,        TradeTab_Text,        activeIndex == 0);
        SetTab(TradeRecordTab_Button,  TradeRecordTab_Text,  activeIndex == 1);
        SetTab(ChipsRequestTab_Button, ChipsRequestTab_Text, activeIndex == 2);
    }

    private void SetTab(Button btn, TextMeshProUGUI _, bool active)
    {
        if (TabActive_Sprite != null && TabInactive_Sprite != null)
            btn.image.sprite = active ? TabActive_Sprite : TabInactive_Sprite;
    }

    public void ShowChipsRequestTab()
    {
        ShowTab(2);
    }

    public void RefreshBadge()
    {
        int count = ClubContext.PendingCount;
        if (ChipsRequestBadge != null)
            ChipsRequestBadge.SetActive(count > 0);
        if (ChipsRequestBadge_Text != null)
            ChipsRequestBadge_Text.text = count > 99 ? "99+" : count.ToString();
    }

    private void OnBackTap()
    {
        gameObject.SetActive(false);
    }

}
