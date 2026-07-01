using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One export-type row: name + diamond cost + toggle. Built from ExportTypeData.
/// </summary>
public class ExportTypeRowView : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI costText;

    public string TypeKey { get; private set; }
    public int Cost { get; private set; }
    public bool IsOn => toggle != null && toggle.isOn;

    public void Setup(ExportTypeData data, Action onChanged)
    {
        TypeKey = data.TypeKey;
        Cost = data.Cost;

        if (nameText != null) nameText.text = data.DisplayName;
        if (costText != null) costText.text = data.Cost.ToString();

        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(data.DefaultOn);
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(_ => onChanged?.Invoke());
        }
    }
}
