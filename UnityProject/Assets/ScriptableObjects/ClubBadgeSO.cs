using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ClubBadgeImage", menuName = "ClubPoker/ClubBadgeImage")]
public class ClubBadgeSO : ScriptableObject
{
    public List<ClubBadgeData> ClubBadges = new List<ClubBadgeData>();
}

[System.Serializable]
public class ClubBadgeData
{
    public string BadgeName;
    public Sprite BadgeImage;
}