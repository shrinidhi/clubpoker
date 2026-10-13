using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class BadBeatWinnerPanel : MonoBehaviour
{
    public Transform Content;
    public GameObject BadBeatWinnerPrefab;
    public GameObject NoDataAvailable;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    private void OnEnable()
    {
        Reload();
    }

    public void Reload()
    {
        if (!isActiveAndEnabled)
            return;

        ClearRows();

        if (NoDataAvailable != null)
            NoDataAvailable.SetActive(false);

        LoadWinnersAsync(ClubContext.ClubId).Forget();
    }

    private async UniTask LoadWinnersAsync(string clubId)
    {
        try
        {
            await UniTask.Yield();
            var response = await ClubManager.Instance.GetClubJackpotWinnersAsync(clubId);
            if (response.Winners != null)
            {
                foreach (var winner in response.Winners)
                {
                    if (winner == null)
                        continue;

                    GameObject row = Instantiate(BadBeatWinnerPrefab, Content);
                    spawnedRows.Add(row);
                    row.GetComponent<BadBeatWinnerPrefabScript>().SetData(winner);
                    row.SetActive(true);
                }
            }

            bool empty = spawnedRows.Count == 0;
            NoDataAvailable.SetActive(empty);
            await UniTask.NextFrame();
            Canvas.ForceUpdateCanvases();

            if (Content is RectTransform contentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }
        catch (Exception e)
        {
            ClearRows();
            NoDataAvailable.SetActive(false);
            Debug.LogError("[Jackpot Winners] Load failed: " + e.Message);
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

}