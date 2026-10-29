
using UnityEngine;
using UnityEngine.UI;

public class FilterDropDownPrefab : MonoBehaviour
{
    public Button PrefabButton;
    public Text FilterName;
    public GameObject TickMark;
    public GameObject UpArrow;
    public GameObject DownArrow;

    private MemberPanelScript memberPanel;
    private AgentPanelScript agentPanel;
    private int optionIndex;

    public void Setup(string label, int index, MemberPanelScript owner)
    {
        optionIndex = index;
        memberPanel = owner;
        agentPanel = null;

        SetupCommon(label);
    }

    public void Setup(string label, int index, AgentPanelScript owner)
    {
        optionIndex = index;
        agentPanel = owner;
        memberPanel = null;

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

        if (TickMark != null)
            TickMark.SetActive(selected);

        if (UpArrow != null)
            UpArrow.SetActive(selected && !descending);

        if (DownArrow != null)
            DownArrow.SetActive(selected && descending);
    }

    private void OnDestroy()
    {
        if (PrefabButton != null)
            PrefabButton.onClick.RemoveListener(OnFilterClicked);
    }
}
