using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class TransactionPrefabScript : MonoBehaviour
{
    public Image Transaction_TypeIcon;
    public Text Transaction_amountText;
    public Text Transaction_descriptionText;
    public Text Transaction_dateText;

    public Color creditColor = Color.green;
    public Color debitColor = Color.red;

    public Sprite bonusIcon;
    public Sprite buyinIcon;
    public Sprite winIcon;
    public Sprite loseIcon;

    private string handId;

    public void Setup(TransactionData data)
    {
        Transaction_descriptionText.text = data.description;
        Transaction_dateText.text = data.date;
        handId = data.handId;

        Transaction_amountText.text = (data.amount > 0 ? "+" : "") + data.amount;

        if (data.amount > 0)
            Transaction_amountText.color = creditColor;
        else
            Transaction_amountText.color = debitColor;

      
        switch (data.type)
        {
            case "BONUS":
                Transaction_TypeIcon.sprite = bonusIcon;
                break;
            case "BUYIN":
                Transaction_TypeIcon.sprite = buyinIcon;
                break;
            case "WIN":
                Transaction_TypeIcon.sprite = winIcon;
                break;
            case "LOSE":
                Transaction_TypeIcon.sprite = loseIcon;
                break;
        }
    }

  
    public void OnClick()
    {
        if (!string.IsNullOrEmpty(handId))
        {
            Debug.Log("Open Replay for HandId: " + handId);
           
        }
    }
}
