using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ClubPoker.Core;

public class JoinUnionScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public InputField EnterUnionID_InputField;
    public Button JoinButton;
    public GameObject ClubUpgradeScreen;


    public ClubUnionData JoinedUnion { get; private set; }

    public event Action<ClubUnionData> OnUnionJoined;

    private bool busy;
    private int viewVersion;
    public GameObject UnionClubScreen;
    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (JoinButton != null)
            JoinButton.onClick.AddListener(JoinButtonOnTap);
    }

    private void OnEnable()
    {
        ++viewVersion;

        if (!busy)
        {
            JoinedUnion = null;

            if (EnterUnionID_InputField != null)
                EnterUnionID_InputField.SetTextWithoutNotify("");

            SetMessage("");
        }

        UpdateControls();
    }

    private void OnDisable()
    {
        ++viewVersion;
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private void CloseButtonOnTap()
    {
        if (busy)
            return;

        gameObject.SetActive(false);
    }

    private void JoinButtonOnTap()
    {
        if (busy)
            return;

        string unionCode = EnterUnionID_InputField != null
            ? EnterUnionID_InputField.text.Trim()
            : "";

        if (string.IsNullOrWhiteSpace(unionCode))
        {
            SetMessage(Loc.Get("union.enter_code"));
            return;
        }

        string clubId = ClubContext.ClubId;

        if (string.IsNullOrWhiteSpace(clubId))
        {
            SetMessage(Loc.Get("something_went_wrong"));
            return;
        }

        JoinUnionAsync(
            unionCode,
            clubId,
            viewVersion).Forget();
    }

    private async UniTask JoinUnionAsync(
        string unionCode,
        string clubId,
        int version)
    {
        busy = true;
        UpdateControls();

        ClubUnionData joinedUnion = null;

        try
        {
            if (ClubManager.Instance == null)
            {
                throw new InvalidOperationException(
                    "ClubManager is missing.");
            }

            var clubResponse =
                await ClubManager.Instance.GetClubDetailAsync(clubId);

            if (!IsCurrent(version))
                return;

            if (clubResponse == null || clubResponse.Club == null)
            {
                throw new InvalidOperationException(
                    "Club details were empty.");
            }

            var club = clubResponse.Club;

            bool hasActiveLevel =
                club.ClubLevel > 0 &&
                DateTimeOffset.TryParse(
                    club.ClubLevelExpiresAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out DateTimeOffset expiresAt) &&
                expiresAt > DateTimeOffset.UtcNow;

            if (!hasActiveLevel)
            {
                if (ClubUpgradeScreen != null)
                    ClubUpgradeScreen.SetActive(true);

                return;
            }

            SetMessage(Loc.Get("union.joining"));

            var response =
                await ClubManager.Instance.JoinClubUnionAsync(
                    unionCode,
                    clubId);

            joinedUnion = response.Union;
        }
        catch (Exception e)
        {
            if (IsCurrent(version))
                SetMessage(e.Message);

            Debug.LogError("[Union] Join failed: " + e.Message);
        }
        finally
        {
            if (this != null)
            {
                busy = false;
                UpdateControls();
            }
        }

        if (this == null || joinedUnion == null)
            return;

        JoinedUnion = joinedUnion;

        if (IsCurrent(version))
        {
            var detail = await ClubManager.Instance.GetClubDetailAsync(clubId);
            ClubContext.SetClubDetail(detail?.Club);
            UnionClubScreen.SetActive(false);
            SetMessage(Loc.Get("union.joined"));
            gameObject.SetActive(false);
        }

        OnUnionJoined?.Invoke(joinedUnion);
    }

    private void UpdateControls()
    {
        if (CloseButton != null)
            CloseButton.interactable = !busy;

        if (JoinButton != null)
            JoinButton.interactable = !busy;

        if (EnterUnionID_InputField != null)
            EnterUnionID_InputField.interactable = !busy;
    }

    private void SetMessage(string message)
    {
        InformationPrefabScript.Instance.ShowMessage(message);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (JoinButton != null)
            JoinButton.onClick.RemoveListener(JoinButtonOnTap);
    }
}