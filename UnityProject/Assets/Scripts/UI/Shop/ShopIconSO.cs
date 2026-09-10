using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Icons shown on the shop tiles. Each tier says "from this amount upward, use this
/// sprite", so a pack's art follows its size — small pile, big pile, chest — without any
/// per-package wiring. Add a package on the server and it picks up the right icon on its own.
///
/// Tiers may be listed in any order; the highest MinAmount that a package meets wins.
/// </summary>
[CreateAssetMenu(fileName = "ShopIcons", menuName = "ClubPoker/ShopIcons")]
public class ShopIconSO : ScriptableObject
{
    [Tooltip("Matched against a gold pack's gold amount.")]
    public List<ShopIconTier> GoldTiers = new List<ShopIconTier>();

    [Tooltip("Matched against a diamond pack's total diamonds (base + bonus).")]
    public List<ShopIconTier> DiamondTiers = new List<ShopIconTier>();

    public Sprite GetGoldIcon(long amount)
    {
        return Resolve(GoldTiers, amount);
    }

    public Sprite GetDiamondIcon(long amount)
    {
        return Resolve(DiamondTiers, amount);
    }

    /// <summary>Highest tier the amount reaches; the lowest tier is the fallback.</summary>
    private static Sprite Resolve(List<ShopIconTier> tiers, long amount)
    {
        if (tiers == null || tiers.Count == 0)
            return null;

        ShopIconTier best = null;

        foreach (ShopIconTier tier in tiers)
        {
            if (tier == null || tier.Icon == null)
                continue;

            if (amount < tier.MinAmount)
                continue;

            if (best == null || tier.MinAmount > best.MinAmount)
                best = tier;
        }

        if (best != null)
            return best.Icon;

        // Below every tier — use the cheapest one rather than showing nothing.
        ShopIconTier lowest = null;

        foreach (ShopIconTier tier in tiers)
        {
            if (tier == null || tier.Icon == null)
                continue;

            if (lowest == null || tier.MinAmount < lowest.MinAmount)
                lowest = tier;
        }

        return lowest != null ? lowest.Icon : null;
    }
}

[System.Serializable]
public class ShopIconTier
{
    [Tooltip("Smallest pack size that uses this icon.")]
    public long MinAmount;

    public Sprite Icon;
}
