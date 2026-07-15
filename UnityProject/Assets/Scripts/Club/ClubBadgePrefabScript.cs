using System;
using UnityEngine;
using UnityEngine.UI;

public class ClubBadgePrefabScript : MonoBehaviour
{
    public Button ClubBadge_Button;
    public Image ClubBadge_Image;

    public GameObject TickMark;

    private string badgeKey;
    private Action<ClubBadgePrefabScript, string> _onSelect;

    public string BadgeKey => badgeKey;

    public void Setup(ClubBadgeData data, Action<ClubBadgePrefabScript, string> onSelect)
    {
        _onSelect = onSelect;
        badgeKey = data.BadgeName.ToLower();

        ClubBadge_Image.sprite = data.BadgeImage;
        TickMark.SetActive(false);

        ClubBadge_Button.onClick.RemoveAllListeners();
        ClubBadge_Button.onClick.AddListener(() => _onSelect?.Invoke(this, badgeKey));
    }

    public void SetSelected(bool isSelected)
    {
        TickMark.SetActive(isSelected);
    }
}