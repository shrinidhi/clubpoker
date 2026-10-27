using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

public class CareerFavoriteHandPrefab : MonoBehaviour
{
    public Button PrefabButton;
    public Button Favorite_UnselectButton;
    public Text TableName;
    public Text VariantName;
    public Text Smallbig_Blind_Text;
    public Transform CardContent;
    public GameObject CardPrefab;
    public Text NetResulatText;
    public Text DateTime_Text;
    public Sprite SelectFavorite_Sprite;
    public Sprite UnSelectFavorite_Sprite;

    public CareerFavoriteData Data { get; private set; }
    public CareerHandHistoryItem NavigationHand { get; private set; }

    private CareerHandHistoryItem summary;
    private List<CareerHandHistoryItem> allHands;
    private CareerHandHistoryPanel historyPanel;
    private CareerFavoriteDetailData detail;
    private string summaryBlinds;

    private void Awake()
    {
        if (PrefabButton != null) PrefabButton.onClick.AddListener(PrefabButtonOnTap);
        if (Favorite_UnselectButton != null)
            Favorite_UnselectButton.onClick.AddListener(FavoriteUnselectButtonOnTap);
    }

    private void OnEnable()
    {
        CareerFavoriteHands.OnFavoriteChanged += OnFavoriteChanged;
        CareerFavoriteHands.OnBusyChanged += OnBusyChanged;
        RefreshFavorite();
    }

    private void OnDisable()
    {
        CareerFavoriteHands.OnFavoriteChanged -= OnFavoriteChanged;
        CareerFavoriteHands.OnBusyChanged -= OnBusyChanged;
    }

    public void SetData(CareerFavoriteData favorite,
        List<CareerHandHistoryItem> hands, CareerHandHistoryPanel panel)
    {
        Data = favorite;
        allHands = hands;
        historyPanel = panel;
        summary = favorite != null ? CareerFavoriteHands.GetSummary(favorite.HandId) : null;
        detail = null;
        summaryBlinds = favorite != null ? CareerFavoriteHands.GetBlinds(favorite.HandId) : null;
        Populate();

        if (PrefabButton != null)
            PrefabButton.interactable = favorite != null && panel != null;
        RefreshFavorite();
    }

    public void SetDetail(CareerFavoriteDetailData value)
    {
        detail = value;
        Populate();
    }

    public void SetSummary(CareerHandHistoryItem hand, string blindsLabel)
    {
        if (Data == null || hand == null || hand.HandId != Data.HandId) return;
        summary = hand;
        CareerFavoriteHands.Remember(hand);
        if (!string.IsNullOrWhiteSpace(blindsLabel))
        {
            summaryBlinds = blindsLabel;
            CareerFavoriteHands.RememberBlinds(hand.HandId, blindsLabel);
        }
        Populate();
    }

    private void Populate()
    {
        if (Data == null) return;

        string tableName = !string.IsNullOrWhiteSpace(summary?.TableName)
            ? summary.TableName : detail?.TableName;
        string variant = !string.IsNullOrWhiteSpace(summary?.Variant)
            ? summary.Variant : detail?.Variant;
        string playedAt = !string.IsNullOrWhiteSpace(summary?.PlayedAt)
            ? summary.PlayedAt : detail?.PlayedAt;

        if (string.IsNullOrWhiteSpace(playedAt) && detail != null && detail.Timestamp > 0)
        {
            try
            {
                var timestamp = detail.Timestamp > 100000000000L
                    ? DateTimeOffset.FromUnixTimeMilliseconds(detail.Timestamp)
                    : DateTimeOffset.FromUnixTimeSeconds(detail.Timestamp);
                playedAt = timestamp.ToString("o", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException) { }
        }

        int? net = summary != null ? (int?)summary.NetResult : detail?.NetResult;
        List<string> cards = GetMyCards();

        NavigationHand = new CareerHandHistoryItem
        {
            HandId = Data.HandId,
            TableId = !string.IsNullOrWhiteSpace(summary?.TableId)
                ? summary.TableId : detail?.TableId,
            TableName = tableName,
            Variant = variant,
            PlayedAt = playedAt,
            YourCards = cards,
            IsFavorite = true,
            NetResult = net ?? 0
        };

        if (allHands != null)
        {
            int index = allHands.FindIndex(h => h != null && h.HandId == Data.HandId);
            if (index >= 0) allHands[index] = NavigationHand;
        }

        SetText(TableName, tableName);
        SetText(VariantName, FormatVariant(variant));
        SetText(Smallbig_Blind_Text, !string.IsNullOrWhiteSpace(detail?.BlindsLabel)
            ? detail.BlindsLabel : summaryBlinds);
        SetText(DateTime_Text, !string.IsNullOrWhiteSpace(playedAt)
            ? FormatDate(playedAt) : "Saved: " + FormatDate(Data.SavedAt));

        if (NetResulatText != null)
        {
            NetResulatText.text = net.HasValue
                ? (net.Value > 0 ? "+" : "") +
                  net.Value.ToString("#,0.##", CultureInfo.InvariantCulture)
                : "-";
            NetResulatText.color = !net.HasValue || net.Value == 0 ? Color.white :
                net.Value > 0 ? Color.green : Color.red;
        }

        ClearCards();
        if (CardContent == null || CardPrefab == null || cards == null) return;
        foreach (string card in cards)
        {
            var instance = Instantiate(CardPrefab, CardContent);
            var component = instance.GetComponent<CareerHandHistoryCard>();
            if (component != null) { component.SetCard(card); instance.SetActive(true); }
            else Destroy(instance);
        }
    }

    private List<string> GetMyCards()
    {
        if (summary?.YourCards != null && summary.YourCards.Count > 0)
            return summary.YourCards;
        if (detail?.YourCards != null && detail.YourCards.Count > 0)
            return detail.YourCards;

        string myId = AuthManager.Instance?.Session?.Id;
        if (string.IsNullOrWhiteSpace(myId)) return null;

        if (detail?.Players != null)
            foreach (var player in detail.Players)
                if (player != null && player.Id == myId &&
                    player.HoleCards != null && player.HoleCards.Count > 0)
                    return player.HoleCards;

        if (detail?.ShowdownCards != null)
            foreach (var player in detail.ShowdownCards)
                if (player != null && player.PlayerId == myId)
                    return player.HoleCards;
        return null;
    }

    private void PrefabButtonOnTap()
    {
        if (Data == null || historyPanel == null) return;
        historyPanel.Open(
            allHands != null ? new List<CareerHandHistoryItem>(allHands) :
                new List<CareerHandHistoryItem> { NavigationHand },
            Data.HandId);
    }

    private void FavoriteUnselectButtonOnTap()
    {
        if (Data == null || CareerFavoriteHands.IsBusy(Data.HandId)) return;
        RemoveAsync(Data.HandId).Forget();
    }

    private async UniTask RemoveAsync(string handId)
    {
        try { await CareerFavoriteHands.SetFavoriteAsync(handId, false); }
        catch (Exception e)
        {
            Debug.LogError("[Favorite Hands] Remove failed: " + e.Message);
            if (this != null && InformationPrefabScript.Instance != null)
                InformationPrefabScript.Instance.ShowMessage(e.Message);
        }
        finally { if (this != null) RefreshFavorite(); }
    }

    private void OnFavoriteChanged(string handId, bool selected)
    {
        if (Data != null && Data.HandId == handId) RefreshFavorite();
    }

    private void OnBusyChanged(string handId)
    {
        if (Data != null && Data.HandId == handId) RefreshFavorite();
    }

    private void RefreshFavorite()
    {
        if (Favorite_UnselectButton == null) return;
        bool valid = Data != null && !string.IsNullOrWhiteSpace(Data.HandId);
        Favorite_UnselectButton.interactable = valid &&
            !CareerFavoriteHands.IsBusy(Data.HandId);
        bool selected = valid && CareerFavoriteHands.IsFavorite(Data.HandId, true);
        CareerHandHistoryPrefab.SetFavoriteSprite(Favorite_UnselectButton,
            selected ? SelectFavorite_Sprite : UnSelectFavorite_Sprite);
    }

    private static string FormatVariant(string value)
    {
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "texas_holdem": case "nlh": return "NLH";
            case "omaha": case "plo": case "plo4": return "PLO4";
            case "omaha_five": case "plo5": return "PLO5";
            case "omaha_six": case "plo6": return "PLO6";
            default: return value;
        }
    }

    private static string FormatDate(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var date)
            ? date.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
            : "-";
    }

    private static void SetText(Text target, string value)
    {
        if (target == null) return;
        target.supportRichText = false;
        target.text = string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void ClearCards()
    {
        if (CardContent == null) return;
        for (int i = CardContent.childCount - 1; i >= 0; i--)
        {
            var obj = CardContent.GetChild(i).gameObject;
            obj.SetActive(false);
            Destroy(obj);
        }
    }

    private void OnDestroy()
    {
        if (PrefabButton != null) PrefabButton.onClick.RemoveListener(PrefabButtonOnTap);
        if (Favorite_UnselectButton != null)
            Favorite_UnselectButton.onClick.RemoveListener(FavoriteUnselectButtonOnTap);
    }
}
