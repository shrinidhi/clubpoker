using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class BadBeatRecordScreenScript : MonoBehaviour
{
    public Button CloseButton;
    public Transform Content;
    public GameObject BadBeatRecordPrefab;

    public string ClubId;

    private readonly List<GameObject> spawnedRows =
        new List<GameObject>();

    private int viewVersion;

    private void Awake()
    {
        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseButtonOnTap);
    }

    private void OnEnable()
    {
        ClubId = ClubContext.ClubId;
        Reload();
    }

    private void OnDisable()
    {
        ++viewVersion;
    }

    public void Reload()
    {
        if (!isActiveAndEnabled)
            return;

        ClearRows();

        LoadRecordsAsync(++viewVersion, ClubId).Forget();
    }

    private bool IsCurrent(int version)
    {
        return this != null &&
               isActiveAndEnabled &&
               version == viewVersion;
    }

    private async UniTask LoadRecordsAsync(int version, string clubId)
    {
        try
        {
            await UniTask.Yield();

            if (!IsCurrent(version))
                return;

            if (ClubManager.Instance == null)
                throw new InvalidOperationException(
                    "ClubManager is missing.");

            if (Content == null || BadBeatRecordPrefab == null)
                throw new InvalidOperationException(
                    "Assign Content and BadBeatRecordPrefab.");

            if (BadBeatRecordPrefab.GetComponent<
                    global::BadBeatRecordPrefab>() == null)
            {
                throw new InvalidOperationException(
                    "BadBeatRecordPrefab component is missing.");
            }

            var response =
                await ClubManager.Instance
                    .GetClubJackpotTransactionsAsync(clubId);

            if (!IsCurrent(version))
                return;

            if (response == null)
                throw new InvalidOperationException(
                    "Transactions response was empty.");

            if (response.Transactions != null)
            {
                foreach (var transaction in response.Transactions)
                {
                    if (transaction == null)
                        continue;

                    GameObject row =
                        Instantiate(BadBeatRecordPrefab, Content);

                    spawnedRows.Add(row);

                    row.GetComponent<global::BadBeatRecordPrefab>()
                        .SetData(transaction);

                    row.SetActive(true);
                }
            }

           

            await UniTask.NextFrame();

            if (!IsCurrent(version))
                return;

            Canvas.ForceUpdateCanvases();

            if (Content is RectTransform contentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }
        catch (Exception e)
        {
            if (!IsCurrent(version))
                return;

            ClearRows();
          

            Debug.LogError(
                "[Jackpot Records] Load failed: " + e.Message);
        }
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

    private void CloseButtonOnTap()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(CloseButtonOnTap);
    }
}