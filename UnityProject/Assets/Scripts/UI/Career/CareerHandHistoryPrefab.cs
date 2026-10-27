using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

public class CareerHandHistoryPrefab : MonoBehaviour
{
    public Transform CardContent;
    public GameObject CardPrefab;
    public Text NetResult;
    public Button PrefabButton;

    private CareerHandHistoryItem handData;
    private CareerHandHistoryPanel historyPanel;
    private List<CareerHandHistoryItem> allHands;

    public Button Favorite_SelectHandButton;
    public Sprite SelectFavorite_Sprite;
    public Sprite UnSelectFavorite_Sprite;

    private void Awake()
    {
        if (PrefabButton != null) PrefabButton.onClick.AddListener(OpenHandDetail);
        if (Favorite_SelectHandButton != null)
            Favorite_SelectHandButton.onClick.AddListener(FavoriteButtonOnTap);
    }

    private void OnEnable()
    {
        CareerFavoriteHands.OnFavoriteChanged += OnFavoriteChanged;
        CareerFavoriteHands.OnBusyChanged += OnFavoriteBusyChanged;
        RefreshFavorite();
    }

    private void OnDisable()
    {
        CareerFavoriteHands.OnFavoriteChanged -= OnFavoriteChanged;
        CareerFavoriteHands.OnBusyChanged -= OnFavoriteBusyChanged;
    }

    public void SetData(CareerHandHistoryItem data,
        List<CareerHandHistoryItem> hands, CareerHandHistoryPanel panel)
    {
        handData = data;
        allHands = hands;
        historyPanel = panel;
        ClearCards();

        CareerFavoriteHands.Remember(data);
        RefreshFavorite();

        if (PrefabButton != null) PrefabButton.interactable = data != null && panel != null;
        if (NetResult != null)
        {
            NetResult.text = data == null ? "-" :
                (data.NetResult > 0 ? "+" : "") + data.NetResult.ToString(CultureInfo.InvariantCulture);
            NetResult.color = data == null || data.NetResult == 0 ? Color.white :
                data.NetResult > 0 ? Color.green : Color.red;
        }
        if (data == null || data.YourCards == null ||
            CardContent == null || CardPrefab == null) return;

        foreach (string card in data.YourCards)
        {
            GameObject obj = Instantiate(CardPrefab, CardContent);
            var row = obj.GetComponent<CareerHandHistoryCard>();
            if (row != null) { row.SetCard(card); obj.SetActive(true); }
            else Destroy(obj);
        }
    }

    private void OpenHandDetail()
    {
        if (handData == null || historyPanel == null) return;
        historyPanel.Open(allHands, handData.HandId);
    }

    private void FavoriteButtonOnTap()
    {
        if (handData == null || CareerFavoriteHands.IsBusy(handData.HandId)) return;
        ToggleFavoriteAsync(handData.HandId,
            !CareerFavoriteHands.IsFavorite(handData.HandId, handData.IsFavorite)).Forget();
    }

    private async UniTask ToggleFavoriteAsync(string handId, bool selected)
    {
        try { await CareerFavoriteHands.SetFavoriteAsync(handId, selected); }
        catch (Exception e)
        {
            Debug.LogError("[Favorite Hands] Update failed: " + e.Message);
            if (this != null && InformationPrefabScript.Instance != null)
                InformationPrefabScript.Instance.ShowMessage(e.Message);
        }
        finally { if (this != null) RefreshFavorite(); }
    }

    private void OnFavoriteChanged(string handId, bool selected)
    {
        if (handData == null || handData.HandId != handId) return;
        handData.IsFavorite = selected;
        RefreshFavorite();
    }

    private void OnFavoriteBusyChanged(string handId)
    {
        if (handData != null && handData.HandId == handId) RefreshFavorite();
    }

    private void RefreshFavorite()
    {
        if (Favorite_SelectHandButton == null) return;
        bool valid = handData != null && !string.IsNullOrWhiteSpace(handData.HandId);
        Favorite_SelectHandButton.interactable = valid &&
            !CareerFavoriteHands.IsBusy(handData.HandId);
        bool selected = valid &&
            CareerFavoriteHands.IsFavorite(handData.HandId, handData.IsFavorite);
        Sprite sprite = selected ? SelectFavorite_Sprite : UnSelectFavorite_Sprite;
        SetFavoriteSprite(Favorite_SelectHandButton, sprite);
    }

    public static void SetFavoriteSprite(Button button, Sprite sprite)
    {
        if (button == null || button.image == null || sprite == null) return;
        button.image.sprite = sprite;
        if (button.transition == Selectable.Transition.SpriteSwap)
        {
            var state = button.spriteState;
            state.highlightedSprite = sprite;
            state.pressedSprite = sprite;
            state.selectedSprite = sprite;
            state.disabledSprite = sprite;
            button.spriteState = state;
        }
    }

    private void ClearCards()
    {
        if (CardContent == null) return;
        for (int i = CardContent.childCount - 1; i >= 0; i--)
        {
            var child = CardContent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void OnDestroy()
    {
        if (PrefabButton != null) PrefabButton.onClick.RemoveListener(OpenHandDetail);
        if (Favorite_SelectHandButton != null)
            Favorite_SelectHandButton.onClick.RemoveListener(FavoriteButtonOnTap);
    }
}
