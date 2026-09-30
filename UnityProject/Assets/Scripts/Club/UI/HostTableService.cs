using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;

/// <summary>
/// Keeps the host settings in <see cref="TableContext.Info"/> in step with the
/// server. The snapshot taken on join goes stale (another device, auto-extend
/// ticking credits down, a cold start that only restored ids), so the host
/// screens re-read it before showing anything.
/// </summary>
public static class HostTableService
{
    /// <summary>
    /// Re-read our club row. There is no single-table GET, so this pulls the
    /// club's table list and picks our row out of it. Failures are swallowed by
    /// GetClubTablesAsync — the screens keep showing the last known values.
    /// </summary>
    public static async UniTask RefreshAsync()
    {
        TableInfo info = TableContext.Info;

        if (info == null ||
            string.IsNullOrEmpty(info.ClubId) ||
            string.IsNullOrEmpty(info.ClubTableRowId))
            return;

        var rows = await AuthManager.Instance.GetClubTablesAsync(info.ClubId);

        foreach (ClubTableData row in rows)
        {
            if (row.Id != info.ClubTableRowId) continue;

            TableContext.ApplyClubRow(row);
            return;
        }
    }
}
