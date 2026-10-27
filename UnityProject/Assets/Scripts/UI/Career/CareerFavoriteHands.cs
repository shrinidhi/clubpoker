using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ClubPoker.Auth;
using ClubPoker.Networking.Models;
using UnityEngine;

// Shares confirmed favorite state between history rows and the favorites screen.
public static class CareerFavoriteHands
{
    public static event Action<string, bool> OnFavoriteChanged;
    public static event Action<string> OnBusyChanged;

    private static string userId;
    private static readonly Dictionary<string, bool> states = new Dictionary<string, bool>();
    private static readonly Dictionary<string, CareerHandHistoryItem> summaries =
        new Dictionary<string, CareerHandHistoryItem>();
    private static readonly Dictionary<string, string> blinds =
        new Dictionary<string, string>();
    private static readonly HashSet<string> pending = new HashSet<string>();

    private static string EnsureUser()
    {
        string current = AuthManager.Instance?.Session?.Id;
        if (current != userId)
        {
            userId = current;
            states.Clear();
            summaries.Clear();
            blinds.Clear();
        }
        return current;
    }

    private static string PendingKey(string playerId, string handId)
    {
        return playerId + "|" + handId;
    }

    public static void Remember(CareerHandHistoryItem hand)
    {
        EnsureUser();
        if (hand == null || string.IsNullOrWhiteSpace(hand.HandId)) return;
        summaries[hand.HandId] = hand;
        if (states.TryGetValue(hand.HandId, out bool value))
            hand.IsFavorite = value;
        else
            states[hand.HandId] = hand.IsFavorite;
    }

    public static CareerHandHistoryItem GetSummary(string handId)
    {
        EnsureUser();
        return summaries.TryGetValue(handId, out var data) ? data : null;
    }

    public static void RememberBlinds(string handId, string label)
    {
        EnsureUser();
        if (!string.IsNullOrWhiteSpace(handId) && !string.IsNullOrWhiteSpace(label))
            blinds[handId] = label;
    }

    public static string GetBlinds(string handId)
    {
        EnsureUser();
        return handId != null && blinds.TryGetValue(handId, out string label) ? label : null;
    }

    public static bool IsFavorite(string handId, bool fallback = false)
    {
        EnsureUser();
        return handId != null && states.TryGetValue(handId, out bool value) ? value : fallback;
    }

    public static bool IsBusy(string handId)
    {
        return pending.Contains(PendingKey(EnsureUser(), handId));
    }

    public static void RememberSaved(string handId)
    {
        EnsureUser();
        states[handId] = true;
        if (summaries.TryGetValue(handId, out var hand)) hand.IsFavorite = true;
    }

    public static async UniTask SetFavoriteAsync(string handId, bool selected)
    {
        string owner = EnsureUser();
        if (string.IsNullOrWhiteSpace(owner))
            throw new InvalidOperationException("Player session is not ready.");
        if (string.IsNullOrWhiteSpace(handId))
            throw new ArgumentException("Hand ID is required.");

        string key = PendingKey(owner, handId);
        if (!pending.Add(key)) return;
        NotifyBusy(handId);

        try
        {
            var manager = AuthManager.Instance;
            if (manager == null) throw new InvalidOperationException("AuthManager is not ready.");

            if (selected)
                await manager.SaveCareerFavoriteAsync(handId);
            else
                await manager.RemoveCareerFavoriteAsync(handId);

            await UniTask.SwitchToMainThread();
            if (EnsureUser() != owner) return;

            states[handId] = selected;
            if (summaries.TryGetValue(handId, out var hand)) hand.IsFavorite = selected;

            if (OnFavoriteChanged != null)
                foreach (Action<string, bool> listener in OnFavoriteChanged.GetInvocationList())
                    try { listener(handId, selected); }
                    catch (Exception e) { Debug.LogError("[Favorite Hands] Listener failed: " + e.Message); }
        }
        finally
        {
            pending.Remove(key);
            if (EnsureUser() == owner) NotifyBusy(handId);
        }
    }

    private static void NotifyBusy(string handId)
    {
        if (OnBusyChanged == null) return;
        foreach (Action<string> listener in OnBusyChanged.GetInvocationList())
            try { listener(handId); }
            catch (Exception e) { Debug.LogError("[Favorite Hands] Listener failed: " + e.Message); }
    }
}
