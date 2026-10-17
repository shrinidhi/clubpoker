using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class UnionUpgradeScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public Button UpgradeLevelButton;
    public GameObject ClubLevelUpgradeScreen;
    // Start is called before the first frame update
    void Start()
    {
        CloseButton.onClick.AddListener(CloseButtonOnTap);
        UpgradeLevelButton.onClick.AddListener(UpgradeLevelButtonOnTap);
    }
     
    void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    void UpgradeLevelButtonOnTap()
    {
        ClubLevelUpgradeScreen.SetActive(true);
    }
}
