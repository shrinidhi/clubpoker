using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;

    public GameObject LeaderboardScreen;

    public Button BackButton;
    public Button RecordButton;
    public Button CreatLeaderBoard_1Button;
    public Button CreateLeaderBoard_2Button;

    public GameObject CeateLeaderboardOptionScreen;
    public string Type;

    public GameObject LeaderBoardPrefab;
    public Transform LeaderBoardContent;

    public GameObject LeaderBoardDataAcontent;
    public GameObject NoDataAvailable;
    public GameObject RecordScreen;

    public event Action<ClubLeaderboardData> OnLeaderboardSelected;

    private readonly List<GameObject> spawnedRows =
        new List<GameObject>();

    private int viewVersion;
    private int layoutVersion;

    private bool loading;
    private bool loadFailed;

    public LeaderboardScoreScreen ScoreScreen;
    public InputField SearchInputfield;
    public ShowClubTableScreenScript ShowClubTableScreenScript;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (SearchInputfield != null)
        {
            SearchInputfield.onValueChanged.AddListener(
                OnSearchChanged);
        }

        if (BackButton != null)
            BackButton.onClick.AddListener(BackButtonOnTap);

        if (RecordButton != null)
            RecordButton.onClick.AddListener(RecordButtonOnTap);

        if (CreatLeaderBoard_1Button != null)
        {
            CreatLeaderBoard_1Button.onClick.AddListener(
                CreateLeaderboard1OnTap);
        }

        if (CreateLeaderBoard_2Button != null)
        {
            CreateLeaderBoard_2Button.onClick.AddListener(
                CreateLeaderboard2OnTap);
        }
    }

    public async void Loadchips()
    {
        try
        {
            if (ClubManager.Instance == null)
                return;

            await ClubManager.Instance.GetChipsSummaryAsync(
                ClubContext.ClubId);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Leaderboards] Chips summary load failed: " + e.Message);
        }
    }

    private void OnEnable()
    {
        ClubRole role =
            ClubContext.ParseRole(ClubContext.SelectedClub.Role);

        bool canCreate =
            role == ClubRole.Creator ||
            (role == ClubRole.Manager && !ClubContext.Is_TableManager);

        if (CreatLeaderBoard_1Button != null)
            CreatLeaderBoard_1Button.gameObject.SetActive(canCreate);

        if (CreateLeaderBoard_2Button != null)
            CreateLeaderBoard_2Button.gameObject.SetActive(canCreate);

        Reload();
    }

    private void OnDisable()
    {
        ++viewVersion;
        ++layoutVersion;
    }

    private void OnSearchChanged(string value)
    {
        if (!isActiveAndEnabled)
            return;

        ApplySearch();
    }

    private void ApplySearch()
    {
        if (loading || loadFailed)
            return;

        string query = SearchInputfield != null
            ? SearchInputfield.text.Trim()
            : "";

        foreach (GameObject row in spawnedRows)
        {
            if (row == null)
                continue;

            var prefab = row.GetComponent<LeaderboardPrefab>();

            string leaderboardName =
                prefab != null && prefab.Data != null
                    ? prefab.Data.Name ?? ""
                    : "";

            bool matches =
                string.IsNullOrEmpty(query) ||
                leaderboardName.IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0;

            row.SetActive(matches);
        }

        // Search only filters rows. It does not change either panel.
        RefreshLayoutAsync(viewVersion).Forget();
    }

    public void Reload()
    {
        if (!isActiveAndEnabled)
            return;

        Loadchips();

        int version = ++viewVersion;

        loading = true;
        loadFailed = false;

        ClearRows();

        // Hide both panels while the API request is loading.
        if (LeaderBoardDataAcontent != null)
            LeaderBoardDataAcontent.SetActive(false);

        if (NoDataAvailable != null)
            NoDataAvailable.SetActive(false);

        SetMessage("Loading...");

        RefreshLayoutAsync(version).Forget();

        LoadLeaderboardsAsync(
            version,
            ClubContext.ClubId).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private async UniTask LoadLeaderboardsAsync(
        int version,
        string clubId)
    {
        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            if (ClubManager.Instance == null)
            {
                throw new InvalidOperationException(
                    "ClubManager is missing.");
            }

            if (LeaderBoardContent == null ||
                LeaderBoardPrefab == null)
            {
                throw new InvalidOperationException(
                    "Assign LeaderBoardContent and LeaderBoardPrefab.");
            }

            if (LeaderBoardPrefab.GetComponent<LeaderboardPrefab>() == null)
            {
                throw new InvalidOperationException(
                    "LeaderboardPrefab component is missing.");
            }

            var leaderboards = new List<ClubLeaderboardData>();
            var loadedIds = new HashSet<string>();

            int page = 1;
            int fetchedCount = 0;

            while (true)
            {
                var response =
                    await ClubManager.Instance.GetClubLeaderboardsAsync(
                        clubId,
                        page,
                        10);

                if (!IsCurrent(version))
                    return;

                if (response == null)
                {
                    throw new InvalidOperationException(
                        "Leaderboards response was empty.");
                }

                var items = response.Leaderboards;

                if (items == null || items.Count == 0)
                    break;

                fetchedCount += items.Count;
                int newItems = 0;

                foreach (var data in items)
                {
                    if (data == null ||
                        string.IsNullOrWhiteSpace(data.Id) ||
                        !loadedIds.Add(data.Id))
                    {
                        continue;
                    }

                    leaderboards.Add(data);
                    ++newItems;
                }

                if (fetchedCount >= response.Total)
                    break;

                if (newItems == 0)
                {
                    throw new InvalidOperationException(
                        "The server returned a repeated leaderboard page.");
                }

                ++page;
            }

            foreach (var data in leaderboards)
            {
                if (data == null ||
                    string.Equals(
                        data.Status,
                        "CANCELLED",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject instance =
                    Instantiate(
                        LeaderBoardPrefab,
                        LeaderBoardContent);

                spawnedRows.Add(instance);

                instance.GetComponent<LeaderboardPrefab>().SetData(
                    data,
                    LeaderboardOnTap);

                instance.SetActive(true);
            }

            loading = false;
            loadFailed = false;

            // Use all generated rows, regardless of the search text.
            bool hasData = spawnedRows.Count > 0;

            if (LeaderBoardDataAcontent != null)
                LeaderBoardDataAcontent.SetActive(hasData);

            if (NoDataAvailable != null)
                NoDataAvailable.SetActive(!hasData);

            SetMessage("");

            // Apply the latest search without changing the panels.
            ApplySearch();
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            loading = false;
            loadFailed = true;

            ClearRows();

            // A failed request is not a successful empty response.
            if (LeaderBoardDataAcontent != null)
                LeaderBoardDataAcontent.SetActive(false);

            if (NoDataAvailable != null)
                NoDataAvailable.SetActive(false);

            SetMessage("Failed to load leaderboards.");

            Debug.LogError(
                "[Leaderboards] Load failed: " + e.Message);

            await RefreshLayoutAsync(version);
        }
    }

    private async UniTask RefreshLayoutAsync(int version)
    {
        int requestedLayoutVersion = ++layoutVersion;

        await UniTask.NextFrame();

        if (!IsCurrent(version) ||
            requestedLayoutVersion != layoutVersion ||
            LeaderBoardContent == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        if (LeaderBoardContent.gameObject.activeInHierarchy)
        {
            var layouts =
                LeaderBoardContent.GetComponentsInChildren<RectTransform>(
                    false);

            for (int i = layouts.Length - 1; i >= 0; i--)
                LayoutRebuilder.ForceRebuildLayoutImmediate(layouts[i]);
        }

        Transform current = LeaderBoardContent.parent;

        while (current != null)
        {
            if (current.gameObject.activeInHierarchy &&
                current is RectTransform rect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }

            if (current.GetComponent<Canvas>() != null)
                break;

            current = current.parent;
        }

        Canvas.ForceUpdateCanvases();
    }

    private void LeaderboardOnTap(ClubLeaderboardData data)
    {
        if (data == null)
            return;

        if (ScoreScreen == null)
        {
            Debug.LogError(
                "[Leaderboards] Assign the leaderboard score screen.");
            return;
        }

        ScoreScreen.Open(ClubContext.ClubId, data.Id);

        OnLeaderboardSelected?.Invoke(data);
    }

    private void BackButtonOnTap()
    {
        if (LeaderboardScreen != null)
            LeaderboardScreen.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void RecordButtonOnTap()
    {
        if (RecordScreen != null)
            RecordScreen.SetActive(true);
    }

    private void CreateLeaderboard1OnTap()
    {
        ShowLeaderBoardOption();
    }

    private void CreateLeaderboard2OnTap()
    {
        ShowLeaderBoardOption();
    }

    private void ShowLeaderBoardOption()
    {
        if (CeateLeaderboardOptionScreen != null)
            CeateLeaderboardOptionScreen.SetActive(true);
    }

    private void ClearRows()
    {
        foreach (GameObject row in spawnedRows)
        {
            if (row == null)
                continue;

            row.SetActive(false);
            Destroy(row);
        }

        spawnedRows.Clear();
    }

    private void SetMessage(string message)
    {
        // Retained for your existing message UI integration.
    }

    private void OnDestroy()
    {
        if (BackButton != null)
            BackButton.onClick.RemoveListener(BackButtonOnTap);

        if (RecordButton != null)
            RecordButton.onClick.RemoveListener(RecordButtonOnTap);

        if (CreatLeaderBoard_1Button != null)
        {
            CreatLeaderBoard_1Button.onClick.RemoveListener(
                CreateLeaderboard1OnTap);
        }

        if (CreateLeaderBoard_2Button != null)
        {
            CreateLeaderBoard_2Button.onClick.RemoveListener(
                CreateLeaderboard2OnTap);
        }

        if (SearchInputfield != null)
        {
            SearchInputfield.onValueChanged.RemoveListener(
                OnSearchChanged);
        }

        if (Instance == this)
            Instance = null;
    }
}