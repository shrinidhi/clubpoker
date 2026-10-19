using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardDeleteConfirmScript : MonoBehaviour
{
    public Button CancelButton;
    public Button CloseButton;
    public Button ConfirmButton;
    private LeaderboardScoreScreen ownerScreen;
    private string clubId;
    private string leaderboardId;

    private bool busy;

    private void Awake()
    {
        if (CancelButton != null)
            CancelButton.onClick.AddListener(CancelButtonOnTap);

        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);

        if (ConfirmButton != null)
            ConfirmButton.onClick.AddListener(ConfirmButtonOnTap);
    }

    public void Open(
        LeaderboardScoreScreen owner,
        string selectedClubId,
        string selectedLeaderboardId)
    {
        if (busy)
            return;

        if (string.IsNullOrWhiteSpace(selectedClubId) ||
            string.IsNullOrWhiteSpace(selectedLeaderboardId))
        {
            Debug.LogError(
                "[Leaderboard Delete] Club ID and leaderboard ID are required.");
            return;
        }

        ownerScreen = owner;
        clubId = selectedClubId;
        leaderboardId = selectedLeaderboardId;

        gameObject.SetActive(true);

        SetMessage("");
        SetBusy(false);
    }

    private void CancelButtonOnTap()
    {
        if (!busy)
            gameObject.SetActive(false);
    }

    private void CloseButtonOnTap()
    {
        if (!busy)
            gameObject.SetActive(false);
    }

    private void ConfirmButtonOnTap()
    {
        if (busy)
            return;

        DeleteAsync().Forget();
    }

    private async UniTask DeleteAsync()
    {
        SetBusy(true);
        SetMessage("Deleting leaderboard...");

        string requestClubId = clubId;
        string requestLeaderboardId = leaderboardId;

        try
        {
            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            await ClubManager.Instance.DeleteClubLeaderboardAsync(
                requestClubId,
                requestLeaderboardId);
        }
        catch (Exception e)
        {
            if (this != null)
            {
                SetMessage(e.Message);
                SetBusy(false);

                Debug.LogError(e.Message);
            }

            return;
        }

        // The server has confirmed cancellation.
        if (this == null)
            return;

        SetBusy(false);
        SetMessage("");
        gameObject.SetActive(false);

        if (ownerScreen != null)
        {
            ownerScreen.OnLeaderboardDeleted(
                requestClubId,
                requestLeaderboardId);
        }

        if (LeaderboardManager.Instance != null)
            LeaderboardManager.Instance.Reload();
    }

    private void SetBusy(bool value)
    {
        busy = value;

        if (CancelButton != null)
            CancelButton.interactable = !value;

        if (CloseButton != null)
            CloseButton.interactable = !value;

        if (ConfirmButton != null)
        {
            ConfirmButton.interactable =
                !value &&
                !string.IsNullOrWhiteSpace(clubId) &&
                !string.IsNullOrWhiteSpace(leaderboardId);
        }
    }

    private void SetMessage(string message)
    {
        InformationPrefabScript.Instance.ShowMessage(message);
    }

    private void OnDestroy()
    {
        if (CancelButton != null)
            CancelButton.onClick.RemoveListener(CancelButtonOnTap);

        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);

        if (ConfirmButton != null)
            ConfirmButton.onClick.RemoveListener(ConfirmButtonOnTap);
    }
}