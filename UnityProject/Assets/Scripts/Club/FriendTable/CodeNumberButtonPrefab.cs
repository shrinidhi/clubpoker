using System;
using UnityEngine;
using UnityEngine.UI;

public class CodeNumberButtonPrefab : MonoBehaviour
{
    public Button CodeNuberButton;
    public Text NumberText;

    private string buttonValue;
    private Action<string> clickCallback;

    public void SetData(
        string value,
        Action<string> callback)
    {
        buttonValue = value;
        clickCallback = callback;

        if (NumberText != null)
            NumberText.text = value;

        if (CodeNuberButton != null)
        {
            CodeNuberButton.onClick
                .RemoveAllListeners();

            CodeNuberButton.onClick
                .AddListener(OnButtonClick);
        }
    }

    private void OnButtonClick()
    {
        clickCallback?.Invoke(buttonValue);
    }
}