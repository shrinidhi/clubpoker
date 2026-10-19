using UnityEngine;
using UnityEngine.UI;

public class UnionClubPrefab : MonoBehaviour
{
    public Text ClubName;
    public Text ClubCode;

    public void SetData(UnionClubData data)
    {
        SetText(ClubName, data != null ? data.Name : null);
        SetText(ClubCode, data != null ? data.ClubCode : null);
    }

    private static void SetText(Text target, string value)
    {
        if (target == null)
            return;

        target.supportRichText = false;
        target.text = string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}