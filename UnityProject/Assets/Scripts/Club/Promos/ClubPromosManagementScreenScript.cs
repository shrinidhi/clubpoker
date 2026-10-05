using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ClubPromosManagementScreenScript : MonoBehaviour
{
    public Button BackButton;
    public Button TemplatesButton;
    public Button AnalyticsButton;
    public Button CreateNewPromo;

    public Text ActiveSlot_Text;
    public Text ChipsReservedText;
    public Text ClubPoolText;
    public Text ScheduledText;

    public Transform LivePromoContent;
    public GameObject LivePromoPrefab;

    public Transform AllPromosContent;
    public GameObject AllPromosPrefab;

    public string ClubId;

    public Button CreatePromoButton;
    public GameObject CreatePromoScreen;

    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private int viewVersion;


    public Transform content;

    private void Awake()
    {
        if (BackButton != null)
            BackButton.onClick.AddListener(Back);

        CreatePromoButton.onClick.AddListener(CreatePromoButtonOnTap);
    }


    void CreatePromoButtonOnTap()
    {
        CreatePromoScreen.SetActive(true);
    }

    private void OnEnable()
    {
        ClubId = ClubContext.ClubId;
        Reload();
    }
        private void OnDisable() => ++viewVersion;

    private void OnDestroy()
    {
        if (BackButton != null)
            BackButton.onClick.RemoveListener(Back);
    }

    public void Open(string clubId)
    {
        if (string.IsNullOrWhiteSpace(clubId))
        {
            Debug.LogError("[Promos] Club ID is required.");
            return;
        }

        ClubId = clubId;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        else if (isActiveAndEnabled)
            Reload();
    }

    public void Reload()
    {
        if (!isActiveAndEnabled) return;

        ClearRows();
        SetText(ActiveSlot_Text, "—");
        SetText(ChipsReservedText, "—");
        SetText(ClubPoolText, "—");
        SetText(ScheduledText, "—");

        LoadAsync(++viewVersion).Forget();
    }

    private bool IsCurrent(int version) =>
        this != null && isActiveAndEnabled && version == viewVersion;

    private async UniTask LoadAsync(int version)
    {
        // Allow scene managers to complete Awake first.
        await UniTask.Yield();
        if (!IsCurrent(version)) return;

        try
        {
            if (string.IsNullOrWhiteSpace(ClubId))
                throw new InvalidOperationException("Set ClubId through Open(clubId).");

            if (ClubManager.Instance == null)
                throw new InvalidOperationException("ClubManager is missing.");

            ValidateReferences();

            string clubId = ClubId;

            var results = await UniTask.WhenAll(
                ClubManager.Instance.GetClubPromosAsync(clubId),
                ClubManager.Instance.GetClubDetailAsync(clubId)
            );

            if (!IsCurrent(version)) return;

            var promosResponse = results.Item1;
            var clubResponse = results.Item2;

            if (clubResponse == null || clubResponse.Club == null)
                throw new InvalidOperationException("Club detail response was empty.");

            var promos = promosResponse.Promos ?? new List<ClubPromoData>();

            int activeCount = 0;
            int scheduledCount = 0;
            decimal reservedChips = 0;

            foreach (var promo in promos)
            {
                if (promo == null) continue;

                reservedChips += promo.ReservedChips;

                if (string.Equals(promo.Status, "SCHEDULED",
                    StringComparison.OrdinalIgnoreCase))
                    ++scheduledCount;

                if (string.Equals(promo.Status, "ACTIVE",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ++activeCount;

                    var liveRow = Instantiate(LivePromoPrefab, LivePromoContent);
                    spawnedRows.Add(liveRow);
                    liveRow.SetActive(true);
                    liveRow.GetComponent<global::LivePromoPrefab>().Setup(promo);
                }

                var allRow = Instantiate(AllPromosPrefab, AllPromosContent);
                spawnedRows.Add(allRow);
                allRow.SetActive(true);
                allRow.GetComponent<AllPromoPrefab>().Setup(promo);
            }

            SetText(ActiveSlot_Text, activeCount.ToString());
            SetText(ScheduledText, scheduledCount.ToString());
            SetText(ChipsReservedText,
                reservedChips.ToString("#,0.##", CultureInfo.InvariantCulture));
            SetText(ClubPoolText,
                clubResponse.Club.ChipPool.ToString("#,0", CultureInfo.InvariantCulture));
            await RefreshContentAsync();
        }
        catch (Exception e)
        {
            if (IsCurrent(version))
                Debug.LogError("[Promos] Load failed: " + e.Message);
        }
    }

    private void ValidateReferences()
    {
        if (LivePromoContent == null || AllPromosContent == null ||
            LivePromoPrefab == null || AllPromosPrefab == null)
            throw new InvalidOperationException("Assign both prefabs and Content references.");

        if (LivePromoPrefab.GetComponent<global::LivePromoPrefab>() == null ||
            AllPromosPrefab.GetComponent<AllPromoPrefab>() == null)
            throw new InvalidOperationException("Promo prefab components are missing.");
    }

    private void ClearRows()
    {
        foreach (var row in spawnedRows)
        {
            if (row == null) continue;
            row.SetActive(false);
            Destroy(row);
        }

        spawnedRows.Clear();
    }

    private void Back() => gameObject.SetActive(false);

    private static void SetText(Text target, string value)
    {
        if (target != null) target.text = value;
    }
    private async UniTask RefreshContentAsync()
    {
        await UniTask.NextFrame();
        Canvas.ForceUpdateCanvases();

        if (LivePromoContent is RectTransform liveRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(liveRect);

        if (AllPromosContent is RectTransform allRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(allRect);

        if (content is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        Canvas.ForceUpdateCanvases();
    }


}