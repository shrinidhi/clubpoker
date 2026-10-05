using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class AllPromoPrefab : MonoBehaviour
{
    public Text BetName;
    public Text ScopeText;
    public Text TypeText;
    public Text PrizeText;
    public Text PlayersCountText;
    public Text StatusText;
    public Button EditButton;
    public CreatePromoScreenScript PromoEditor;
    private ClubPromoData currentPromo;

    private void Awake()
    {
        if (EditButton != null) EditButton.onClick.AddListener(EditPromo);
    }

    private void OnDestroy()
    {
        if (EditButton != null) EditButton.onClick.RemoveListener(EditPromo);
    }

    public void Setup(ClubPromoData promo)
    {
        currentPromo = promo;
        if (BetName != null)
        {
            BetName.supportRichText = false;
            BetName.text = promo != null ? promo.Name ?? "" : "";
        }
        if (ScopeText != null) ScopeText.text = promo != null ? (promo.Scope ?? "").Replace("_", " ") : "";
        if (TypeText != null) TypeText.text = promo != null ? (promo.PromoType ?? "").Replace("_", " ") : "";
        if (PrizeText != null) PrizeText.text = promo != null ? promo.PrizeAmount.ToString("#,0.##", CultureInfo.InvariantCulture) : "";
        if (PlayersCountText != null) PlayersCountText.text = promo != null ? promo.ParticipantCount.ToString() : "0";
        if (StatusText != null) StatusText.text = promo != null ? promo.Status ?? "" : "";
        if (EditButton != null)
        {
            bool draft = promo != null && string.Equals(promo.Status, "DRAFT", StringComparison.OrdinalIgnoreCase);
            EditButton.gameObject.SetActive(draft);
            EditButton.interactable = draft;
        }
    }

    private void EditPromo()
    {
        if (currentPromo == null || !string.Equals(currentPromo.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)) return;
        var management = GetComponentInParent<ClubPromosManagementScreenScript>();
        if (PromoEditor == null && management != null && management.CreatePromoScreen != null)
            PromoEditor = management.CreatePromoScreen.GetComponentInChildren<CreatePromoScreenScript>(true);
        if (PromoEditor == null)
        {
            ShowMessage("Assign CreatePromoScreen on the parent promo management screen or assign PromoEditor.");
            return;
        }
        if (management != null) PromoEditor.PromosManagementScreen = management;
        string clubId = !string.IsNullOrWhiteSpace(currentPromo.ClubId) ? currentPromo.ClubId
            : management != null ? management.ClubId : ClubContext.ClubId;
        PromoEditor.OpenEdit(clubId, currentPromo.Id);
    }

    private static void ShowMessage(string message)
    {
        if (InformationPrefabScript.Instance != null) InformationPrefabScript.Instance.ShowMessage(message);
        else Debug.LogWarning("[Promos] " + message);
    }
}
