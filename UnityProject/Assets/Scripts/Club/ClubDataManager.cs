using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using ClubPoker.Networking;

/// <summary>
/// Club Data / Export API access. Singleton mirroring ClubChipManager — views
/// (ClubDataPanelScript, ExportDataModalScript) go through this, not ApiClient directly.
/// </summary>
public class ClubDataManager : MonoBehaviour
{
    public static ClubDataManager Instance { get; private set; }

    private ApiClient _api;

    // Scene-scoped (ClubScene only) — no DontDestroyOnLoad, so it can live on the
    // ClubViewController GameObject without dragging the whole scene root persistent.
    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        _api = ApiClient.Instance;
    }

    // ── Export ────────────────────────────────────────────────────────────

    public async UniTask<ExportDataResponse> ExportClubDataAsync(string clubId, ExportDataRequest req)
    {
        return await _api.Post<ExportDataResponse>($"/api/clubs/{clubId}/data/export", req);
    }

    // ── Data (stats + game list) ──────────────────────────────────────────

    public async UniTask<ClubDataResponse> GetClubDataAsync(
        string clubId, DateTime from, DateTime to, string variant)
    {
        string query =
            $"/api/clubs/{clubId}/data?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&variant={variant}";
        return await _api.Get<ClubDataResponse>(query);
    }
}
