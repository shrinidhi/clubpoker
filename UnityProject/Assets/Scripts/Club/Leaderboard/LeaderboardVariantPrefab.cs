using UnityEngine;
using UnityEngine.UI;

public class LeaderboardVariantPrefab : MonoBehaviour
{
    public Toggle VarianToggle;
    public Text VariantName;

    public string VariantCode { get; private set; }

    public bool IsSelected =>
        VarianToggle != null && VarianToggle.isOn;

    public void SetData(string code, string label, bool selected)
    {
        VariantCode = code;

        if (VariantName != null)
        {
            VariantName.supportRichText = false;
            VariantName.text = label;
        }

        if (VarianToggle != null)
        {
            // Each variant can be selected independently.
            VarianToggle.group = null;
            VarianToggle.SetIsOnWithoutNotify(selected);
        }
    }

    public void SetBusy(bool busy)
    {
        if (VarianToggle != null)
            VarianToggle.interactable = !busy;
    }
}