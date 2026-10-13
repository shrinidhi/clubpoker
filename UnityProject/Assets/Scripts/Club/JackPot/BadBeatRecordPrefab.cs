using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class BadBeatRecordPrefab : MonoBehaviour
{
    public Text ClubName;
    public Text DateTime;
    public Text ChipsCount;

    public Color TopUpColor = new Color32(0, 180, 70, 255);
    public Color WithdrawColor = new Color32(220, 40, 40, 255);
    public Color DefaultColor = Color.white;

    public void SetData(ClubJackpotTransactionData data)
    {
        if (ClubName != null)
        {
            ClubName.supportRichText = false;
            ClubName.text = data != null &&
                            !string.IsNullOrWhiteSpace(data.ActorName)
                ? data.ActorName
                : "-";
        }

        if (DateTime != null)
        {
            DateTime.text = "-";

            if (data != null &&
                DateTimeOffset.TryParse(
                    data.CreatedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out DateTimeOffset date))
            {
                DateTime.text = date.ToLocalTime().ToString(
                    "dd/MM/yyyy HH:mm",
                    CultureInfo.InvariantCulture);
            }
        }

        if (ChipsCount == null)
            return;

        ChipsCount.supportRichText = false;
        ChipsCount.color = DefaultColor;
        ChipsCount.text = "-";

        if (data == null)
            return;

        string amount = Math.Abs(data.Amount).ToString(
            "#,0.##", CultureInfo.InvariantCulture);

        if (string.Equals(
                data.Type, "TOP_UP",
                StringComparison.OrdinalIgnoreCase))
        {
            ChipsCount.text = "+" + amount;
            ChipsCount.color = TopUpColor;
        }
        else if (string.Equals(
                     data.Type, "WITHDRAW",
                     StringComparison.OrdinalIgnoreCase))
        {
            ChipsCount.text = "-" + amount;
            ChipsCount.color = WithdrawColor;
        }
        else
        {
            ChipsCount.text = data.Amount.ToString(
                "#,0.##", CultureInfo.InvariantCulture);
        }
    }
}