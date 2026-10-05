using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class LivePromoPrefab : MonoBehaviour
{
    public Button PrefabButton;
    public Text BetName;
    public Text PriceAmountText;
    public Text Participants_ScopeText;
    public Button EndButton;
    public Button PauseButton;

    public void Setup(ClubPromoData promo)
    {
        if (BetName != null)
        {
            BetName.supportRichText = false;
            BetName.text = promo.Name ?? "";
        }

        if (PriceAmountText != null)
            PriceAmountText.text = promo.PrizeAmount.ToString(
                "#,0.##", CultureInfo.InvariantCulture);

        if (Participants_ScopeText != null)
            Participants_ScopeText.text =
                promo.ParticipantCount + " players • " +
                (promo.Scope ?? "").Replace("_", " ");

        // Wire these when their APIs are supplied.
        if (PrefabButton != null) PrefabButton.interactable = false;
        if (EndButton != null) EndButton.interactable = false;
        if (PauseButton != null) PauseButton.interactable = false;
    }
}