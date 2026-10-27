using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using UnityEngine;
using UnityEngine.UI;

public class CareerFavoriteHandScreen : MonoBehaviour
{
    public Button CloseButton;
    public Transform Content;
    public GameObject CareerFavoriteHandPrefab;

    [Header("References")]
    public CareerHandHistoryPanel CareerHandHistoryPanel;

    [Header("Optional")]
    public GameObject NoDataAvailable;
    public GameObject LoadingPanel;
    public Text MessageText;

    private readonly List<global::CareerFavoriteHandPrefab> rows =
        new List<global::CareerFavoriteHandPrefab>();
    private readonly List<CareerHandHistoryItem> hands = new List<CareerHandHistoryItem>();
    private int viewVersion;

    private void Awake()
    {
        if (CloseButton != null) CloseButton.onClick.AddListener(CloseButtonOnTap);
    }

    private void OnEnable()
    {
        CareerFavoriteHands.OnFavoriteChanged += OnFavoriteChanged;
        Reload();
    }

    private void OnDisable()
    {
        ++viewVersion;
        CareerFavoriteHands.OnFavoriteChanged -= OnFavoriteChanged;
    }

    public void Reload()
    {
        if (!isActiveAndEnabled) return;
        int version = ++viewVersion;
        ClearRows();
        SetActive(NoDataAvailable, false);
        SetActive(LoadingPanel, true);
        SetMessage("Loading favorite hands...");
        LoadAsync(version).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null && isActiveAndEnabled && version == viewVersion;
    }

    private async UniTask LoadAsync(int version)
    {
        try
        {
            await UniTask.Yield();
            if (!IsCurrent(version)) return;
            if (Content == null || CareerFavoriteHandPrefab == null ||
                CareerFavoriteHandPrefab.GetComponent<global::CareerFavoriteHandPrefab>() == null)
                throw new InvalidOperationException("Assign Content and the favorite hand prefab.");
            if (AuthManager.Instance == null)
                throw new InvalidOperationException("AuthManager is not ready.");

            string owner = AuthManager.Instance.Session?.Id;
            if (string.IsNullOrWhiteSpace(owner))
                throw new InvalidOperationException("Player session is not ready.");

            var favorites = new List<CareerFavoriteData>();
            var ids = new HashSet<string>();
            int page = 1;
            int fetched = 0;

            while (true)
            {
                var response = await AuthManager.Instance.GetCareerFavoritesAsync(page, 50);
                if (!IsCurrent(version) || !IsOwner(owner)) return;
                if (response.Favorites == null || response.Favorites.Count == 0) break;

                fetched += response.Favorites.Count;
                int added = 0;
                foreach (var item in response.Favorites)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.HandId) ||
                        !ids.Add(item.HandId)) continue;
                    favorites.Add(item);
                    ++added;
                }
                if (fetched >= response.Total) break;
                if (added == 0)
                    throw new InvalidOperationException("The server returned a repeated favorites page.");
                ++page;
            }

            foreach (var item in favorites)
                hands.Add(new CareerHandHistoryItem { HandId = item.HandId, IsFavorite = true });

            foreach (var item in favorites)
            {
                CareerFavoriteHands.RememberSaved(item.HandId);
                var instance = Instantiate(CareerFavoriteHandPrefab, Content);
                var row = instance.GetComponent<global::CareerFavoriteHandPrefab>();
                rows.Add(row);
                row.SetData(item, hands, CareerHandHistoryPanel);
                instance.SetActive(true);
            }

            bool empty = rows.Count == 0;
            SetActive(NoDataAvailable, empty);
            SetMessage(empty && NoDataAvailable == null ? "No favorite hands found." : "");
            await RefreshLayoutAsync(version);

            // Limit detail requests to four at a time and retain the API's favorite order.
            int nextIndex = 0;
            int failed = 0;
            var detailsById = new Dictionary<string, CareerFavoriteDetailData>();
            async UniTask LoadDetailsWorker()
            {
                while (IsCurrent(version) && IsOwner(owner))
                {
                    int index = nextIndex++;
                    if (index >= rows.Count) return;
                    var row = rows[index];
                    try
                    {
                        var detail = await AuthManager.Instance
                            .GetCareerFavoriteDetailAsync(favorites[index].HandId);
                        if (!IsCurrent(version) || !IsOwner(owner)) return;
                        detailsById[favorites[index].HandId] = detail;
                        if (row != null) row.SetDetail(detail);
                    }
                    catch (Exception e)
                    {
                        if (!IsCurrent(version)) return;
                        ++failed;
                        Debug.LogWarning("[Favorite Hands] Detail failed: " + e.Message);
                    }
                }
            }

            var workers = new List<UniTask>();
            for (int i = 0; i < Mathf.Min(4, favorites.Count); i++)
                workers.Add(LoadDetailsWorker());
            await UniTask.WhenAll(workers);

            if (!IsCurrent(version) || !IsOwner(owner)) return;

            // Resolve summaries from the API even when GameDataScript was never opened.
            int unresolved = await LoadSummariesAsync(
                favorites, detailsById, version, owner);

            if (!IsCurrent(version) || !IsOwner(owner)) return;
            if (unresolved > 0)
                SetMessage("Some saved hands are unavailable in career history.");
            else if (failed > 0)
                SetMessage("Some detailed hand information could not be loaded.");
            else
                SetMessage("");

            await RefreshLayoutAsync(version);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version)) return;
            ClearRows();
            SetActive(NoDataAvailable, false);
            SetMessage("Failed to load favorite hands.");
            Debug.LogError("[Favorite Hands] Load failed: " + e.Message);
            await RefreshLayoutAsync(version);
        }
        finally
        {
            if (IsCurrent(version)) SetActive(LoadingPanel, false);
        }
    }

    private async UniTask<int> LoadSummariesAsync(
        List<CareerFavoriteData> favorites,
        Dictionary<string, CareerFavoriteDetailData> detailsById,
        int version,
        string owner)
    {
        var wanted = new HashSet<string>();
        var favoriteIds = new HashSet<string>();
        var rowByHand = new Dictionary<string, global::CareerFavoriteHandPrefab>();
        var prioritizedTables = new List<string>();
        var tableIds = new HashSet<string>();

        foreach (var row in rows)
            if (row != null && row.Data != null)
                rowByHand[row.Data.HandId] = row;

        foreach (var favorite in favorites)
        {
            favoriteIds.Add(favorite.HandId);
            var summary = CareerFavoriteHands.GetSummary(favorite.HandId);
            detailsById.TryGetValue(favorite.HandId, out var detail);
            string blind = !string.IsNullOrWhiteSpace(detail?.BlindsLabel)
                ? detail.BlindsLabel : CareerFavoriteHands.GetBlinds(favorite.HandId);

            if (summary != null)
            {
                if (rowByHand.TryGetValue(favorite.HandId, out var row))
                    row.SetSummary(summary, blind);
                if (!string.IsNullOrWhiteSpace(summary.TableId) && tableIds.Add(summary.TableId))
                    prioritizedTables.Add(summary.TableId);
            }

            if (!string.IsNullOrWhiteSpace(detail?.TableId) && tableIds.Add(detail.TableId))
                prioritizedTables.Add(detail.TableId);

            // A summary has the per-hand net result; a detail-only row may not.
            if (summary == null || string.IsNullOrWhiteSpace(summary.TableName) ||
                string.IsNullOrWhiteSpace(blind))
                wanted.Add(favorite.HandId);
        }

        if (wanted.Count == 0) return 0;
        SetMessage("Loading saved hand information...");

        // The same all-time sessions source used by CareerScreenScript.
        var overview = await AuthManager.Instance.GetCareerOverviewAsync("total", "ALL");
        if (!IsCurrent(version) || !IsOwner(owner)) return wanted.Count;

        var sessionsByTable = new Dictionary<string, CareerSessionData>();
        if (overview?.Sessions != null)
        {
            foreach (var session in overview.Sessions)
            {
                if (session == null || string.IsNullOrWhiteSpace(session.TableId)) continue;
                if (!sessionsByTable.TryGetValue(session.TableId, out var existing) ||
                    (string.IsNullOrWhiteSpace(existing.BlindsLabel) &&
                     !string.IsNullOrWhiteSpace(session.BlindsLabel)))
                    sessionsByTable[session.TableId] = session;
                if (tableIds.Add(session.TableId))
                    prioritizedTables.Add(session.TableId);
            }
        }

        // Reuse a known hand summary while filling its missing table metadata
        // from the fresh overview response.
        foreach (var favorite in favorites)
        {
            var summary = CareerFavoriteHands.GetSummary(favorite.HandId);
            if (summary == null || string.IsNullOrWhiteSpace(summary.TableId) ||
                !sessionsByTable.TryGetValue(summary.TableId, out var session)) continue;
            if (string.IsNullOrWhiteSpace(summary.TableName))
                summary.TableName = session.TableName;
            if (string.IsNullOrWhiteSpace(summary.Variant))
                summary.Variant = session.Variant;
            detailsById.TryGetValue(favorite.HandId, out var detail);
            string blind = !string.IsNullOrWhiteSpace(detail?.BlindsLabel)
                ? detail.BlindsLabel : session.BlindsLabel;
            if (string.IsNullOrWhiteSpace(blind))
                blind = CareerFavoriteHands.GetBlinds(favorite.HandId);
            CareerFavoriteHands.RememberBlinds(favorite.HandId, blind);
            if (rowByHand.TryGetValue(favorite.HandId, out var row) && row != null)
                row.SetSummary(summary, blind);
            if (!string.IsNullOrWhiteSpace(summary.TableName) &&
                !string.IsNullOrWhiteSpace(blind))
                wanted.Remove(favorite.HandId);
        }

        int nextTable = 0;
        async UniTask LoadTableWorker()
        {
            while (IsCurrent(version) && IsOwner(owner) && wanted.Count > 0)
            {
                int index = nextTable++;
                if (index >= prioritizedTables.Count) return;
                string tableId = prioritizedTables[index];
                sessionsByTable.TryGetValue(tableId, out var session);
                var seenHands = new HashSet<string>();
                int page = 1;

                try
                {
                    while (IsCurrent(version) && IsOwner(owner) && wanted.Count > 0)
                    {
                        var response = await AuthManager.Instance
                            .GetCareerHandHistoryPageAsync(tableId, page, 50);
                        if (!IsCurrent(version) || !IsOwner(owner)) return;
                        if (response.Items == null || response.Items.Count == 0) break;
                        int added = 0;

                        foreach (var hand in response.Items)
                        {
                            if (hand == null || string.IsNullOrWhiteSpace(hand.HandId) ||
                                !seenHands.Add(hand.HandId)) continue;
                            ++added;
                            if (!favoriteIds.Contains(hand.HandId)) continue;

                            hand.IsFavorite = true;
                            if (string.IsNullOrWhiteSpace(hand.TableId)) hand.TableId = tableId;
                            if (string.IsNullOrWhiteSpace(hand.TableName))
                                hand.TableName = session?.TableName;
                            if (string.IsNullOrWhiteSpace(hand.Variant))
                                hand.Variant = session?.Variant;

                            detailsById.TryGetValue(hand.HandId, out var detail);
                            string blind = !string.IsNullOrWhiteSpace(detail?.BlindsLabel)
                                ? detail.BlindsLabel : session?.BlindsLabel;
                            if (string.IsNullOrWhiteSpace(blind))
                                blind = CareerFavoriteHands.GetBlinds(hand.HandId);

                            CareerFavoriteHands.Remember(hand);
                            CareerFavoriteHands.RememberBlinds(hand.HandId, blind);
                            if (rowByHand.TryGetValue(hand.HandId, out var row) && row != null)
                                row.SetSummary(hand, blind);

                            if (!string.IsNullOrWhiteSpace(hand.TableName) &&
                                !string.IsNullOrWhiteSpace(blind))
                                wanted.Remove(hand.HandId);
                        }

                        await RefreshLayoutAsync(version);
                        if (!IsCurrent(version) || !IsOwner(owner)) return;

                        if (response.Pagination == null || !response.Pagination.HasMore) break;
                        if (added == 0)
                            throw new InvalidOperationException("The server repeated a hand history page.");
                        ++page;
                    }
                }
                catch (Exception e)
                {
                    if (!IsCurrent(version)) return;
                    Debug.LogWarning("[Favorite Hands] Table history failed: " + e.Message);
                }
            }
        }

        var workers = new List<UniTask>();
        for (int i = 0; i < Mathf.Min(3, prioritizedTables.Count); i++)
            workers.Add(LoadTableWorker());
        await UniTask.WhenAll(workers);
        return wanted.Count;
    }

    private static bool IsOwner(string owner)
    {
        return AuthManager.Instance != null && AuthManager.Instance.Session?.Id == owner;
    }

    private void OnFavoriteChanged(string handId, bool selected)
    {
        Reload();
    }

    private void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    private async UniTask RefreshLayoutAsync(int version)
    {
        await UniTask.NextFrame();
        if (!IsCurrent(version) || Content == null) return;
        Canvas.ForceUpdateCanvases();
        var rects = Content.GetComponentsInChildren<RectTransform>(false);
        for (int i = rects.Length - 1; i >= 0; i--)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rects[i]);
        Transform parent = Content.parent;
        while (parent != null)
        {
            if (parent is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            if (parent.GetComponent<Canvas>() != null) break;
            parent = parent.parent;
        }
        Canvas.ForceUpdateCanvases();
    }

    private void ClearRows()
    {
        foreach (var row in rows)
        {
            if (row == null) continue;
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }
        rows.Clear();
        hands.Clear();
    }

    private void SetMessage(string message)
    {
        if (MessageText == null) return;
        MessageText.supportRichText = false;
        MessageText.text = message;
        MessageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }

    private void OnDestroy()
    {
        CareerFavoriteHands.OnFavoriteChanged -= OnFavoriteChanged;
        if (CloseButton != null) CloseButton.onClick.RemoveListener(CloseButtonOnTap);
    }
}
