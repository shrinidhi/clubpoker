using UnityEngine;
using UnityEngine.UI;

public class ClubBadgePrefabScript : MonoBehaviour
{
    public Button ClubBadge_Button;
    public Image ClubBadge_Image;
  
    public GameObject TickMark;

    private string badgeKey;
    private CreateClubScreenScript manager;

    public void Setup(ClubBadgeData data, CreateClubScreenScript screenScript)
    {
        manager = screenScript;
        badgeKey = data.BadgeName.ToLower();

        ClubBadge_Image.sprite = data.BadgeImage;
        TickMark.SetActive(false);

        ClubBadge_Button.onClick.RemoveAllListeners();
        ClubBadge_Button.onClick.AddListener(OnClickBadge);
    }

    void OnClickBadge()
    {
        manager.SelectBadge(this, badgeKey);
    }

    public void SetSelected(bool isSelected)
    {
        TickMark.SetActive(isSelected);
    }
}