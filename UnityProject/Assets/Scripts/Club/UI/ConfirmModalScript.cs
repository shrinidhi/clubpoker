using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConfirmModalScript : MonoBehaviour
{
    public TextMeshProUGUI Message_Text;
    public Button Confirm_Button;
    public Button Cancel_Button;

    private Action _onConfirm;

    private void Start()
    {
        Confirm_Button.onClick.AddListener(OnConfirmTap);
        Cancel_Button.onClick.AddListener(OnCancelTap);
    }

    public void Show(string message, Action onConfirm)
    {
        _onConfirm = onConfirm;
        Message_Text.text = message;
        gameObject.SetActive(true);
    }

    private void OnConfirmTap()
    {
        gameObject.SetActive(false);
        _onConfirm?.Invoke();
        _onConfirm = null;
    }

    private void OnCancelTap()
    {
        gameObject.SetActive(false);
        _onConfirm = null;
    }
}
