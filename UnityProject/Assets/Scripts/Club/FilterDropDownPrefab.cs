
using UnityEngine;
using UnityEngine.UI;

public class FilterDropDownPrefab : MonoBehaviour
{
    public Button PrefabButton;
    public Text FilterName;
    public GameObject TickMark;
    public Image UpArrow;
    public Image DownArrow;

    private MemberPanelScript memberPanel;
    private AgentPanelScript agentPanel;
    private TradeViewScript tradeView;

    private int optionIndex;

    public void Setup(string label, int index, MemberPanelScript owner)
    {
        optionIndex = index;
        memberPanel = owner;
        agentPanel = null;
        tradeView = null;

        SetupCommon(label);
    }

    public void Setup(string label, int index, AgentPanelScript owner)
    {
        optionIndex = index;
        agentPanel = owner;
        memberPanel = null;
        tradeView = null;

        SetupCommon(label);
    }

    public void Setup(string label, int index, TradeViewScript owner)
    {
        optionIndex = index;
        tradeView = owner;
        memberPanel = null;
        agentPanel = null;

        SetupCommon(label);
    }

    private void SetupCommon(string label)
    {
        if (FilterName != null)
            FilterName.text = label;

        if (PrefabButton != null)
        {
            PrefabButton.onClick.RemoveListener(OnFilterClicked);
            PrefabButton.onClick.AddListener(OnFilterClicked);
        }

        RefreshUI();
    }

    private void OnFilterClicked()
    {
        if (memberPanel != null)
            memberPanel.SelectSortOption(optionIndex);
        else if (agentPanel != null)
            agentPanel.SelectSortOption(optionIndex);
        else if (tradeView != null)
            tradeView.SelectSortOption(optionIndex);
    }

    public void RefreshUI()
    {
        bool selected = false;
        bool descending = false;

        if (memberPanel != null)
        {
            selected = memberPanel.IsSortSelected(optionIndex);
            descending = memberPanel.IsSortDescending(optionIndex);
        }
        else if (agentPanel != null)
        {
            selected = agentPanel.IsSortSelected(optionIndex);
            descending = agentPanel.IsSortDescending(optionIndex);
        }
        else if (tradeView != null)
        {
            selected = tradeView.IsSortSelected(optionIndex);
            descending = tradeView.IsSortDescending(optionIndex);
        }

        if (TickMark != null)
            TickMark.SetActive(selected);

        if (UpArrow != null)
        {
            UpArrow.color = selected && !descending
                ? new Color32(255, 255, 255, 255)
                : new Color32(255, 255, 255, 108);
        }

        if (DownArrow != null)
        {
            DownArrow.color = selected && descending
                ? new Color32(255, 255, 255, 255)
                : new Color32(255, 255, 255, 108);
        }
    }

    private void OnDestroy()
    {
        if (PrefabButton != null)
            PrefabButton.onClick.RemoveListener(OnFilterClicked);
    }
}
