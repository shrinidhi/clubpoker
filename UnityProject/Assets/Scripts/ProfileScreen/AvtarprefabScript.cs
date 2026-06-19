using UnityEngine;
using UnityEngine.UI;
using System;

public class AvtarprefabScript : MonoBehaviour
{
    public Image avatarImage;
    public Button selectButton;
    public GameObject selectedBorder;

    private string avatarName;
    private Action<string> onSelect;

    public void Setup(AvtarData data, Action<string> selectAction)
    {
        avatarName = data.AvtarName;
        onSelect = selectAction;

        if (avatarImage != null)
            avatarImage.sprite = data.AvtarImage;

        if (selectedBorder != null)
            selectedBorder.SetActive(false);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectClicked);
        }
    }

    private void OnSelectClicked()
    {
        onSelect?.Invoke(avatarName);
    }

    public void SetSelected(bool isSelected)
    {
        if (selectedBorder != null)
            selectedBorder.SetActive(isSelected);
    }

    public string GetAvatarName()
    {
        return avatarName;
    }
}