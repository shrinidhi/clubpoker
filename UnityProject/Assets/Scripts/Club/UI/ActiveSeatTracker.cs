using System;
using ClubPoker.Auth;
using ClubPoker.Game;
using ClubPoker.Networking.Models;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The seat the server says we still hold (GET /api/player/active-seat), shared by
/// everything that marks it: the Take Seat button (ReturnToTableBanner) and the
/// active-chips badge on a club table row (ClubTablePrefabScript).
///
/// Overlapping refreshes collapse to the latest answer. A failed call keeps the
/// last known value — offline isn't "seat gone". The value belongs to the user
/// it was fetched for; after a logout / account switch it reads as no seat
/// until the new user's fetch lands.
/// </summary>
public static class ActiveSeatTracker
{
    private static ActiveSeat _seat;
    private static string _ownerId;

    /// Null when we hold no seat (or it hasn't been fetched yet for this user).
    public static ActiveSeat Current =>
        _ownerId != null && _ownerId == AuthManager.Instance?.Session?.Id ? _seat : null;

    /// Fired after every successful refresh, seat or no seat.
    public static event Action<ActiveSeat> Changed;

    private static int _version;
    private static bool _hooked;

    public static async UniTask<ActiveSeat> RefreshAsync()
    {
        Hook();

        AuthManager auth = AuthManager.Instance;
        string userId = auth?.Session?.Id;
        if (string.IsNullOrEmpty(userId))
        {
            Set(null, null);
            return null;
        }

        int version = ++_version;
        ActiveSeat seat;
        try
        {
            seat = await auth.GetActiveSeatAsync();
        }
        catch (Exception e)
        {
            // Offline, or a server on older code with no such route.
            Debug.LogWarning($"[ActiveSeat] active-seat failed: {e.Message}");
            return Current;
        }

        if (version == _version)
            Set(seat, userId);
        return Current;
    }

    /// True when the given club table row (or engine table) is where our seat is.
    public static bool IsSeatedAt(ClubTableData row)
    {
        ActiveSeat seat = Current;
        if (seat == null || row == null)
            return false;

        return (!string.IsNullOrEmpty(seat.ClubTableRowId) && seat.ClubTableRowId == row.Id) ||
               (!string.IsNullOrEmpty(row.TableId) && seat.TableId == row.TableId);
    }

    // A proper leave frees the seat, but /leave runs in the background and the
    // club screen can ask before it lands. Drop the seat locally at once, then
    // confirm with the server.
    private static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        LeaveTableHandler.SeatLeft += OnSeatLeft;
    }

    private static void OnSeatLeft(string tableId)
    {
        ActiveSeat seat = Current;
        if (seat != null && seat.TableId == tableId)
            Set(null, _ownerId);
        RefreshAsync().Forget();
    }

    private static void Set(ActiveSeat seat, string ownerId)
    {
        _seat = seat;
        _ownerId = ownerId;
        Changed?.Invoke(Current);
    }
}
