using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MessagePrefabScript : MonoBehaviour
{
    public TextMeshProUGUI Title_Text;
    public TextMeshProUGUI Body_Text;
    public TextMeshProUGUI Time_Text;
    public GameObject UnreadIndicator;   // dot/badge shown only when unread
    public Button RowButton;             // tap row to open/read

    private ClubMessageData _data;
    private Action<ClubMessageData> _onTap;

    public void Setup(ClubMessageData data, Action<ClubMessageData> onTap = null)
    {
        _data = data;
        _onTap = onTap;

        if (Title_Text != null) Title_Text.text = data.Title;
        if (Body_Text != null)  Body_Text.text = data.Body;
        if (Time_Text != null)  Time_Text.text = data.CreatedAt;

        Refresh();

        if (RowButton != null)
        {
            RowButton.onClick.RemoveListener(OnRowTap);
            RowButton.onClick.AddListener(OnRowTap);
        }
    }

    private void OnRowTap() => _onTap?.Invoke(_data);

    /// <summary>Update the read/unread visuals from the current data.</summary>
    public void Refresh()
    {
        if (UnreadIndicator != null)
            UnreadIndicator.SetActive(_data != null && !_data.IsRead);
    }
}
