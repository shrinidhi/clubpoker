using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardScoreScreen : MonoBehaviour
{
    public Button BackButton;
    public Button DeleteButton;

    public Text PrizePoolText;
    public Text Start_EndDateTime_Text;
    public Text DoubleScore_Text;
    public Text LeaderboardName_Text;
    public Text Description_Text;
    public Text Variant_Text;
    public Text BB_AnteText;

    public GameObject MyPlayerContent;
    public Text MyPlayerName;
    public Image MyPlayer_Image;
    public Text MyPlayerScore;

    public Transform RankContent;
    public Transform ShowRankPrefab;

    public ClubLeaderboardData Data { get; private set; }

    private readonly List<GameObject> spawnedRows =
        new List<GameObject>();

    private string selectedClubId;
    private string selectedLeaderboardId;

    private int viewVersion;
    private int layoutVersion;

    public AvtarSO AvtarSO;
    public LeaderboardDeleteConfirmScript DeleteConfirmScreen;
    public Text Status;

    public string type;
    public Button InformationButton;
    public GameObject InformationScreen;
    private void Awake()
    {
        if (BackButton != null)
            BackButton.onClick.AddListener(BackButtonOnTap);

        if (DeleteButton != null)
        {
            DeleteButton.onClick.AddListener(DeleteButtonOnTap);
        }

        InformationButton.onClick.AddListener(InformationButtonOnTap);
    }

    void InformationButtonOnTap()
    {
        InformationScreen.SetActive(true);
    }

    private void DeleteButtonOnTap()
    {
        if (Data == null)
            return;

        if (string.Equals(
                Data.Status,
                "CANCELLED",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (DeleteConfirmScreen == null)
        {
            Debug.LogError(
                "[Leaderboard Detail] Assign DeleteConfirmScreen.");
            return;
        }

        DeleteConfirmScreen.Open(
            this,
            selectedClubId,
            selectedLeaderboardId);
    }

    public void OnLeaderboardDeleted(
        string deletedClubId,
        string deletedLeaderboardId)
    {
        // Close only if this is still the deleted leaderboard.
        if (!string.Equals(
                selectedClubId,
                deletedClubId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                selectedLeaderboardId,
                deletedLeaderboardId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Data = null;
        gameObject.SetActive(false);
    }

    public void Open(string clubId, string leaderboardId)
    {
        if (string.IsNullOrWhiteSpace(clubId) ||
            string.IsNullOrWhiteSpace(leaderboardId))
        {
            Debug.LogError(
                "[Leaderboard Detail] Club ID and leaderboard ID are required.");
            return;
        }

        selectedClubId = clubId;
        selectedLeaderboardId = leaderboardId;

        gameObject.SetActive(true);
        Reload();
    }

    private void OnDisable()
    {
        ++viewVersion;
        ++layoutVersion;
    }

    public void Reload()
    {
        if (!isActiveAndEnabled)
            return;

        if (string.IsNullOrWhiteSpace(selectedClubId) ||
            string.IsNullOrWhiteSpace(selectedLeaderboardId))
        {
            return;
        }

        int version = ++viewVersion;

        ClearRows();
        ClearDetails();

        RequestLayoutRefresh();

        LoadDetailsAsync(
            version,
            selectedClubId,
            selectedLeaderboardId).Forget();

        LoadMyRankAsync(
            version,
            selectedClubId,
            selectedLeaderboardId).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private async UniTask LoadDetailsAsync(
        int version,
        string clubId,
        string leaderboardId)
    {
        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            ValidateReferences();

            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            var standings = new List<ClubLeaderboardStanding>();
            var loadedPlayers = new HashSet<string>();

            ClubLeaderboardData board = null;

            int page = 1;
            int fetchedCount = 0;

            while (true)
            {
                var response =
                    await ClubManager.Instance
                        .GetClubLeaderboardDetailAsync(
                            clubId,
                            leaderboardId,
                            page,
                            20);

                if (!IsCurrent(version))
                    return;

                if (response == null || response.Leaderboard == null)
                {
                    throw new InvalidOperationException(
                        "Leaderboard detail response was empty.");
                }

                ValidateReturnedLeaderboard(
                    response.Leaderboard,
                    clubId,
                    leaderboardId);

                if (board == null)
                    board = response.Leaderboard;

                var entries = response.Standings;

                if (entries == null || entries.Count == 0)
                    break;

                fetchedCount += entries.Count;
                int addedCount = 0;

                foreach (var entry in entries)
                {
                    if (entry == null ||
                        entry.Rank < 1 ||
                        string.IsNullOrWhiteSpace(entry.PlayerId) ||
                        !loadedPlayers.Add(entry.PlayerId))
                    {
                        continue;
                    }

                    standings.Add(entry);
                    ++addedCount;
                }

                if (fetchedCount >= response.Total)
                    break;

                if (addedCount == 0)
                {
                    throw new InvalidOperationException(
                        "The server returned a repeated standings page.");
                }

                ++page;
            }

            if (!IsCurrent(version))
                return;

            Data = board;

            PopulateDetails(board);
            CreateRankRows(board, standings);

            RequestLayoutRefresh();
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            Data = null;
            ClearRows();

            Debug.LogError(
                "[Leaderboard Detail] Load failed: " + e.Message);

            RequestLayoutRefresh();
        }
    }

    private async UniTask LoadMyRankAsync(
        int version,
        string clubId,
        string leaderboardId)
    {
        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            var response =
                await ClubManager.Instance
                    .GetClubLeaderboardMyRankAsync(
                        clubId,
                        leaderboardId);

            if (!IsCurrent(version))
                return;

            if (response == null)
            {
                throw new InvalidOperationException(
                    "My-rank response was empty.");
            }

            PopulateMyRank(response.Entry);

          

            RequestLayoutRefresh();
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            PopulateMyRank(null);
            Debug.LogError(
                "[Leaderboard Detail] My-rank load failed: " + e.Message);

            RequestLayoutRefresh();
        }
    }

    private void ValidateReferences()
    {
        if (RankContent == null)
            throw new InvalidOperationException(
                "RankContent is not assigned.");

        if (ShowRankPrefab == null)
            throw new InvalidOperationException(
                "ShowRankPrefab is not assigned.");

        if (ShowRankPrefab.GetComponent<global::ShowRankPrefab>() == null)
        {
            throw new InvalidOperationException(
                "ShowRankPrefab component is missing on the rank prefab.");
        }
    }

    private static void ValidateReturnedLeaderboard(
        ClubLeaderboardData board,
        string clubId,
        string leaderboardId)
    {
        if (!string.Equals(
                board.Id,
                leaderboardId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                board.ClubId,
                clubId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The server returned a different club or leaderboard.");
        }
    }

    private void PopulateDetails(ClubLeaderboardData board)
    {
        if (board == null)
            return;

        SetText(LeaderboardName_Text, board.Name);
        SetText(Description_Text, board.Description);
        SetText(PrizePoolText, FormatAmount(board.TotalPrizePool));
        Status.text = board.Status;
        type = board.Type;
        SetText(
            Start_EndDateTime_Text,
            FormatDate(board.StartAt) +
            " - " +
            FormatDate(board.EndAt));

        SetText(
            DoubleScore_Text,
            board.DoubleScores
                ? "Double score: " +
                  board.DoubleScoreHour.ToString(
                      "00",
                      CultureInfo.InvariantCulture) +
                  ":00"
                : "");

        var variants = new List<string>();

        if (board.VariantFilter != null)
        {
            foreach (string variant in board.VariantFilter)
            {
                if (!string.IsNullOrWhiteSpace(variant))
                    variants.Add(variant);
            }
        }

        SetText(
            Variant_Text,
            variants.Count > 0
                ? string.Join(", ", variants)
                : "Games: All");

        SetText(BB_AnteText, FormatBBAmount(board.BbMin) + " - " + FormatBBAmount(board.BbMax));

        if (DeleteButton != null)
            DeleteButton.gameObject.SetActive(CanDeleteLeaderboard());
    }

    private void PopulateMyRank(ClubLeaderboardStanding entry)
    {
        bool hasPlayer = entry != null &&
                         !string.IsNullOrWhiteSpace(entry.PlayerId);

        if (MyPlayerContent != null)
            MyPlayerContent.SetActive(hasPlayer);

        SetText(MyPlayerName, hasPlayer ? entry.Username : "");
        SetText(MyPlayerScore, hasPlayer ? FormatAmount(entry.Metric) : "");
        

        if (MyPlayer_Image != null)
        {
            Sprite avatar = hasPlayer ? FindAvatarSprite(entry.Avatar) : null;
            MyPlayer_Image.sprite = avatar;
            MyPlayer_Image.enabled = avatar != null;
        }
    }

    private Sprite FindAvatarSprite(string avatarName)
    {
        Sprite defaultAvatar = null;

        if (AvtarSO != null && AvtarSO.AvtarBadges != null)
        {
            string key = (avatarName ?? "").Trim();

            foreach (var avatar in AvtarSO.AvtarBadges)
            {
                if (avatar == null || avatar.AvtarImage == null)
                    continue;

                string name = (avatar.AvtarName ?? "").Trim();

                if (key.Length > 0 && string.Equals(
                        name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return avatar.AvtarImage;
                }

                if (string.Equals(
                        name, "default", StringComparison.OrdinalIgnoreCase))
                {
                    defaultAvatar = avatar.AvtarImage;
                }
            }
        }

        return defaultAvatar;
    }

    private static string FormatBBAmount(decimal amount)
    {
        // Keep values below 10,000 as plain numbers.
        if (amount >= 1000000m || amount <= -1000000m)
            return (amount / 1000000m).ToString(
                "0.##", CultureInfo.InvariantCulture) + "M";

        if (amount >= 10000m || amount <= -10000m)
            return (amount / 1000m).ToString(
                "0.##", CultureInfo.InvariantCulture) + "K";

        return amount.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void CreateRankRows(
        ClubLeaderboardData board,
        List<ClubLeaderboardStanding> standings)
    {
        ClearRows();

        var playersByRank =
            new Dictionary<int, List<ClubLeaderboardStanding>>();

        var rewardsByRank = new Dictionary<int, decimal>();

        int lastRank = 0;

        if (board != null && board.Prizes != null)
        {
            foreach (var prize in board.Prizes)
            {
                if (prize == null || prize.Position < 1)
                    continue;

                rewardsByRank[prize.Position] = prize.Amount;
                lastRank = Math.Max(lastRank, prize.Position);
            }
        }

        if (standings != null)
        {
            foreach (var player in standings)
            {
                if (player == null || player.Rank < 1)
                    continue;

                lastRank = Math.Max(lastRank, player.Rank);

                if (!playersByRank.TryGetValue(
                        player.Rank,
                        out List<ClubLeaderboardStanding> players))
                {
                    players = new List<ClubLeaderboardStanding>();
                    playersByRank.Add(player.Rank, players);
                }

                // Keep every player when multiple players share a rank.
                players.Add(player);
            }
        }

        for (int rank = 1; rank <= lastRank; rank++)
        {
            decimal? reward =
                rewardsByRank.TryGetValue(rank, out decimal amount)
                    ? amount
                    : (decimal?)null;

            if (playersByRank.TryGetValue(
                    rank,
                    out List<ClubLeaderboardStanding> players))
            {
                foreach (var player in players)
                    SpawnRankRow(rank, player, reward);
            }
            else
            {
                // Keep the rank visible with an empty player name.
                SpawnRankRow(rank, null, reward);
            }
        }
    }

    private void SpawnRankRow(
        int rank,
        ClubLeaderboardStanding player,
        decimal? reward)
    {
        Transform instance =
            Instantiate(ShowRankPrefab, RankContent);

        spawnedRows.Add(instance.gameObject);

        instance.GetComponent<global::ShowRankPrefab>().SetData(
            rank,
            player,
            reward);

        instance.gameObject.SetActive(true);
    }

    private void RequestLayoutRefresh()
    {
        if (!isActiveAndEnabled)
            return;

        RefreshLayoutAsync(++layoutVersion, viewVersion).Forget();
    }

    private async UniTask RefreshLayoutAsync(
        int requestedLayoutVersion,
        int requestedViewVersion)
    {
        await UniTask.NextFrame();

        if (!IsCurrent(requestedViewVersion) ||
            requestedLayoutVersion != layoutVersion)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        // Rebuild row layouts first, then their parent layouts.
        if (RankContent != null &&
            RankContent.gameObject.activeInHierarchy)
        {
            var layouts =
                RankContent.GetComponentsInChildren<RectTransform>(false);

            for (int i = layouts.Length - 1; i >= 0; i--)
                LayoutRebuilder.ForceRebuildLayoutImmediate(layouts[i]);

            RebuildParents(RankContent.parent);
        }

        // Refresh the detail screen's other text and layout groups.
        if (transform is RectTransform screenRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(screenRect);
            RebuildParents(screenRect.parent);
        }

        Canvas.ForceUpdateCanvases();
    }

    private static void RebuildParents(Transform current)
    {
        while (current != null)
        {
            if (current is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            if (current.GetComponent<Canvas>() != null)
                break;

            current = current.parent;
        }
    }

    private void ClearDetails()
    {
        Data = null;

        SetText(PrizePoolText, "—");
        SetText(Start_EndDateTime_Text, "—");
        SetText(DoubleScore_Text, "—");
        SetText(LeaderboardName_Text, "—");
        SetText(Description_Text, "");
        SetText(Variant_Text, "—");
        SetText(BB_AnteText, "—");

        PopulateMyRank(null);
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

    private void BackButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    private static string FormatAmount(decimal amount)
    {
        return amount.ToString(
            "#,0.##",
            CultureInfo.InvariantCulture);
    }

    private static string FormatDate(string value)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset date))
        {
            return "-";
        }

        return date.ToLocalTime().ToString(
            "MM/dd HH:mm",
            CultureInfo.InvariantCulture);
    }

    private static void SetText(Text target, string value)
    {
        if (target == null)
            return;

        target.supportRichText = false;
        target.text = value ?? "";
    }

    private void OnDestroy()
    {
        if (BackButton != null)
            BackButton.onClick.RemoveListener(BackButtonOnTap);
    }


    private bool CanDeleteLeaderboard()
    {
        ClubRole role = ClubContext.ParseRole(ClubContext.SelectedClub.Role);

        return Data != null &&
               !string.Equals(Data.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(Data.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase) &&
               (role == ClubRole.Creator || (role == ClubRole.Manager && !ClubContext.Is_TableManager));
    }
}