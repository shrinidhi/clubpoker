using UnityEngine;
using UnityEngine.UI;

public class EnterCodeTextPrefab : MonoBehaviour
{
    public Text EnterCodeText;

    public void SetText(string value)
    {
        if (EnterCodeText != null)
            EnterCodeText.text = value;
    }
}