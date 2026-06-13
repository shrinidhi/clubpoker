using UnityEngine;
using UnityEngine.UI;

public class ChangeRoleManagerScript : MonoBehaviour
{
    public Text Header_Text;
    public Toggle ManagerToggle;
    public Toggle TableManagerToggle;
    public Button CancelButton;
    public Button ConfirmButton;

    private System.Action<bool, bool> onConfirm;

    void Start()
    {
        ManagerToggle.onValueChanged.AddListener(delegate { OnManagerToggleChanged(); });
        TableManagerToggle.onValueChanged.AddListener(delegate { OnTableManagerToggleChanged(); });

        CancelButton.onClick.AddListener(() =>
        {
            gameObject.SetActive(false);
        });

        ConfirmButton.onClick.AddListener(() =>
        {
            if (!ManagerToggle.isOn && !TableManagerToggle.isOn)
            {
                Debug.LogWarning("Please select Manager or Table Manager");
                return;
            }

            onConfirm?.Invoke(
                ManagerToggle.isOn,
                TableManagerToggle.isOn
            );

            gameObject.SetActive(false);
        });
    }

    public void Show(
        bool isManagerSelected,
        bool isTableManagerSelected,
        System.Action<bool, bool> confirmCallback)
    {
        onConfirm = confirmCallback;

        if (isTableManagerSelected)
        {
            ManagerToggle.isOn = false;
            TableManagerToggle.isOn = true;
        }
        else
        {
            ManagerToggle.isOn = true;
            TableManagerToggle.isOn = false;
        }

        UpdateHeader();
        gameObject.SetActive(true);
    }

    public void OnManagerToggleChanged()
    {
        if (ManagerToggle.isOn)
            TableManagerToggle.isOn = false;

        UpdateHeader();
    }

    public void OnTableManagerToggleChanged()
    {
        if (TableManagerToggle.isOn)
            ManagerToggle.isOn = false;

        UpdateHeader();
    }

    private void UpdateHeader()
    {
        if (ManagerToggle.isOn)
            Header_Text.text = "Manager";
        else if (TableManagerToggle.isOn)
            Header_Text.text = "Table Manager";
        else
            Header_Text.text = "Select Permission";
    }
}