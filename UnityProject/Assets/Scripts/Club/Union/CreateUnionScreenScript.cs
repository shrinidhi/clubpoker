using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;

public class CreateUnionScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public InputField UnionName;
    public Button CreateUnionButton;
    public GameObject ClubUpgradeScreen;

    public GameObject UnionClubScreen;
    public ClubUnionData CreatedUnion { get; private set; }

    public event Action<ClubUnionData> OnUnionCreated;

    private bool busy;
    private int viewVersion;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (CreateUnionButton != null)
        {
            CreateUnionButton.onClick.AddListener(CreateUnionButtonOnTap);
        }
    }

    private void OnEnable()
    {
        ++viewVersion;

        if (!busy)
        {
            if (UnionName != null)
                UnionName.SetTextWithoutNotify("");

            SetMessage("");
        }

        RefreshControls();
    }

    private void OnDisable()
    {
        ++viewVersion;
    }

    private bool IsCurrent(int version)
    {
        return this != null && isActiveAndEnabled && version == viewVersion;
    }

    private void CloseButtonOnTap()
    {
        if (!busy)
            gameObject.SetActive(false);
    }

    private void CreateUnionButtonOnTap()
    {
        if (busy)
            return;

        string name = UnionName != null
            ? UnionName.text.Trim()
            : "";

        if (string.IsNullOrWhiteSpace(name))
        {
            SetMessage(Loc.Get("union.enter_name"));
            return;
        }

        string clubId = ClubContext.ClubId;

        if (string.IsNullOrWhiteSpace(clubId))
        {
            SetMessage(Loc.Get("something_went_wrong"));
            return;
        }

        CreateUnionAsync(clubId,name,viewVersion).Forget();
    }

    private async UniTask CreateUnionAsync(string clubId,string name,int version)
    {
        busy = true;
        RefreshControls();

        ClubUnionData createdUnion;
        try
        {
            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            var clubResponse = await ClubManager.Instance.GetClubDetailAsync(clubId);

            if (!IsCurrent(version))
                return;

            if (clubResponse == null || clubResponse.Club == null)
                throw new InvalidOperationException(
                    "Club detail response was empty.");

            var club = clubResponse.Club;

            bool activePaidLevel =club.ClubLevel > 0 &&
                DateTimeOffset.TryParse(
                    club.ClubLevelExpiresAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out DateTimeOffset expiresAt) &&
                expiresAt > DateTimeOffset.UtcNow;

            if (!activePaidLevel)
            {
                if (ClubUpgradeScreen != null)
                {
                    ClubUpgradeScreen.SetActive(true);
                }
                else
                {
                    Debug.LogError(
                        "[Create Union] ClubUpgradeScreen is not assigned.");
                }

                return;
            }

            SetMessage(Loc.Get("union.creating"));

            var response =
                await ClubManager.Instance.CreateClubUnionAsync(
                    clubId,
                    name);

            createdUnion = response.Union;
        }
        catch (Exception e)
        {
            if (IsCurrent(version))
                SetMessage(Loc.Get("union.create_failed", ("reason", e.Message)));

            Debug.LogError(
                "[Create Union] Request failed: " + e.Message);

            return;
        }
        finally
        {
            if (this != null)
            {
                busy = false;
                RefreshControls();
            }
        }

        if (this == null)
            return;

        CreatedUnion = createdUnion;

        if (IsCurrent(version))
        {
            var detail = await ClubManager.Instance.GetClubDetailAsync(clubId);
            ClubContext.SetClubDetail(detail?.Club);
           UnionClubScreen.SetActive(false);
            SetMessage(Loc.Get("union.created"));
            gameObject.SetActive(false);
        }

        OnUnionCreated?.Invoke(createdUnion);
    }

    private void RefreshControls()
    {
        if (CloseButton != null)
            CloseButton.interactable = !busy;

        if (CreateUnionButton != null)
            CreateUnionButton.interactable = !busy;

        if (UnionName != null)
            UnionName.interactable = !busy;
    }

    private void SetMessage(string message)
    {
        InformationPrefabScript.Instance.ShowMessage(message);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (CreateUnionButton != null)
        {
            CreateUnionButton.onClick.RemoveListener(
                CreateUnionButtonOnTap);
        }
    }
}